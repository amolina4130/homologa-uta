using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.Connectors;
using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Eventos;
using HomologaUTA.Homologacion.Models;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Homologacion.Services;

/// <summary>
/// Envía la documentación al agente externo y guarda su decisión por asignatura con su justificación.
/// No impone el umbral del 80 %, pero aplica la regla institucional de materias no homologables.
/// Si el agente falla, no se guarda nada: se puede reintentar sin perder datos.
/// Desde HU-21/HU-22 solo se envían las asignaturas candidatas de la preselección y el agente puede
/// proponer una combinación de hasta dos materias de origen.
/// </summary>
public class AnalisisExternoService
{
    private const int MaxJustificacion = 2000;
    private const int MaxNombre = 200;
    private const int MaxDecision = 30;

    private readonly HomologacionDbContext _db;
    private readonly AgenteExternoConnector _conector;
    private readonly PreseleccionService _preseleccion;
    private readonly SolicitudesClient _solicitudes;
    private readonly DocumentosClient _documentos;
    private readonly IAnalisisEventPublisher _publisher;
    private readonly ILogger<AnalisisExternoService> _logger;

    public AnalisisExternoService(
        HomologacionDbContext db,
        AgenteExternoConnector conector,
        PreseleccionService preseleccion,
        SolicitudesClient solicitudes,
        DocumentosClient documentos,
        IAnalisisEventPublisher publisher,
        ILogger<AnalisisExternoService> logger)
    {
        _db = db;
        _conector = conector;
        _preseleccion = preseleccion;
        _solicitudes = solicitudes;
        _documentos = documentos;
        _publisher = publisher;
        _logger = logger;
    }

    /// <summary>
    /// Analiza la solicitud con el agente externo. Si no llega el cuerpo de la petición (o llega
    /// incompleto), se arma con los datos reales de la solicitud y el texto extraído de sus PDF.
    /// </summary>
    /// <exception cref="AnalisisSolicitudException">La solicitud no existe o no tiene documentos procesados.</exception>
    /// <exception cref="AgenteExternoTimeoutException">Venci�� el tiempo de espera; no se guardó nada.</exception>
    /// <exception cref="AgenteExternoException">El agente falló o respondió mal; no se guardó nada.</exception>
    public async Task<ResultadoAnalisisDto> AnalizarSolicitudAsync(
        int solicitudId, AgenteExternoRequest? request, CancellationToken ct = default)
    {
        var info = await _solicitudes.ObtenerCompletaAsync(solicitudId, ct)
                   ?? throw new AnalisisSolicitudException(404, "Solicitud no encontrada.");

        var entrada = await PrepararEntradaAsync(solicitudId, info, request, ct);

        // 1. Enviar al agente. Si falla o vence el tiempo, sale por excepción antes de tocar la base de datos.
        var respuesta = await _conector.EnviarDocumentacionAsync(entrada, ct);
        var filas = respuesta.Asignaturas
            .Select(a => ConstruirFila(a, entrada))
            .ToList();

        // 2. Guardar: si la solicitud ya tenía un análisis, se reemplaza
        var fecha = DateTime.UtcNow;
        var analisis = await _db.AnalisisSolicitudes
            .Include(x => x.Asignaturas)
                .ThenInclude(a => a.MateriasOrigen)
            .FirstOrDefaultAsync(x => x.SolicitudId == solicitudId, ct);

        if (analisis is null)
        {
            analisis = new AnalisisSolicitud { SolicitudId = solicitudId };
            _db.AnalisisSolicitudes.Add(analisis);
        }
        else
        {
            _db.AnalisisAsignaturas.RemoveRange(analisis.Asignaturas.ToList());
            analisis.Asignaturas.Clear();
        }

        analisis.Estado = EstadoAnalisis.PENDIENTE_REVISION;
        analisis.FechaAnalisis = fecha;
        analisis.CoordinadorCorreo = null;
        analisis.FechaInforme = null;
        foreach (var fila in filas)
            analisis.Asignaturas.Add(fila);

        await _db.SaveChangesAsync(ct);

        // 3. Publicar el evento solo después de guardar
        var publicado = false;
        try
        {
            await _publisher.PublicarAnalisisCompletadoAsync(new AnalisisCompletadoEvent
            {
                SolicitudId = solicitudId,
                EstadoFinal = EstadoAnalisis.PENDIENTE_REVISION,
                FechaAnalisis = fecha
            });
            publicado = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo publicar AnalisisCompletado de la solicitud {SolicitudId}", solicitudId);
        }

        // 4. Mover la solicitud a PENDIENTE_REVISION para que aparezca al Coordinador (HU-09).
        var estadoActualizado = false;
        try
        {
            estadoActualizado = await _solicitudes.CambiarEstadoAsync(
                solicitudId, EstadoSolicitudHomologacion.PENDIENTE_REVISION, "sistema@homologauta", ct);
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            _logger.LogError(ex,
                "El análisis externo de {SolicitudId} se guardó, pero no se pudo actualizar su estado", solicitudId);
        }

        return new ResultadoAnalisisDto
        {
            SolicitudId = solicitudId,
            Estado = EstadoAnalisis.PENDIENTE_REVISION,
            FechaAnalisis = fecha,
            EventoPublicado = publicado,
            EstadoSolicitudActualizado = estadoActualizado,
            Asignaturas = filas.Select(f => new ResultadoAnalisisAsignaturaDto
            {
                AsignaturaOrigen = f.AsignaturaOrigen,
                AsignaturaDestino = f.AsignaturaDestino,
                AsignaturaDestinoId = f.AsignaturaDestinoId,
                Combinada = f.Combinada,
                MateriasOrigen = f.MateriasOrigen
                    .Select(m => new MateriaOrigenDto { Nombre = m.Nombre, Nota = m.Nota })
                    .ToList(),
                PorcentajeContenido = f.PorcentajeContenido,
                PorcentajeProfundidad = f.PorcentajeProfundidad,
                PorcentajeHoras = f.PorcentajeHoras,
                Decision = f.Decision,
                Justificacion = f.Justificacion,
                RequiereRevision = f.RequiereRevision
            }).ToList()
        };
    }

    /// <summary>
    /// Completa la petición del agente con los datos reales de la solicitud. Si hay materias
    /// aprobadas, primero hace la preselección por área y nivel (HU-21); si no, usa las
    /// asignaturas de la solicitud (comportamiento anterior).
    /// </summary>
    private async Task<AgenteExternoRequest> PrepararEntradaAsync(
        int solicitudId, SolicitudCompletaDto info, AgenteExternoRequest? request, CancellationToken ct)
    {
        var entrada = request ?? new AgenteExternoRequest();
        entrada.Documentos ??= new DocumentosAgenteExternoDto();
        entrada.SolicitudId = solicitudId;

        if (string.IsNullOrWhiteSpace(entrada.Estudiante))
            entrada.Estudiante = CoordinadorService.NombreEstudiante(info);
        if (string.IsNullOrWhiteSpace(entrada.CarreraOrigen))
            entrada.CarreraOrigen = info.CarreraOrigen;
        if (string.IsNullOrWhiteSpace(entrada.CarreraDestino))
            entrada.CarreraDestino = info.CarreraDestino;
        // Las universidades están disponibles en info (SolicitudCompletaDto) pero el contrato
        // AgenteExternoRequest actual no las incluye; se omiten para no romper el contrato.

        var usaPreseleccion = entrada.MateriasDestino.Count > 0 &&
                              entrada.MateriasDestino.Any(d => d.MateriasOrigenPermitidas.Count > 0);

        if (entrada.MateriasDestino.Count == 0)
        {
            EntradasPreseleccion? preseleccion = null;
            try
            {
                preseleccion = await _preseleccion.ConstruirEntradasAsync(solicitudId, ct);
            }
            catch (AnalisisSolicitudException ex) when (ex.StatusCode == 409)
            {
                // Sin memoria cargada todavía se puede analizar con las asignaturas de la solicitud;
                // la preselección se activará en cuanto la memoria esté disponible.
                _logger.LogWarning(ex,
                    "El análisis externo de {SolicitudId} no pudo usar la preselección; se usan las asignaturas de la solicitud",
                    solicitudId);
            }

            if (preseleccion is { UsaMateriasAprobadas: true, Entradas.Count: > 0 })
            {
                entrada.MateriasDestino = preseleccion.Entradas.Select(ConstruirDestino).ToList();
                entrada.MateriasOrigen = preseleccion.Entradas
                    .SelectMany(e => e.MateriasOrigenPermitidas)
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                usaPreseleccion = true;
            }
            else
            {
                entrada.MateriasOrigen = info.Asignaturas
                    .Select(a => a.AsignaturaOrigen)
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                entrada.MateriasDestino = info.Asignaturas.Select(a => new MateriaDestinoAgenteExternoDto
                {
                    AsignaturaId = a.AsignaturaDestinoId ?? 0,
                    Nombre = a.AsignaturaDestino,
                    Area = a.AreaDestino,
                    Nivel = a.NivelDestino,
                    Contenido = a.ContenidoDestino,
                    MateriasOrigenPermitidas = new List<string> { a.AsignaturaOrigen }
                }).ToList();

                usaPreseleccion = false;
            }
        }
        else if (entrada.MateriasOrigen.Count == 0)
        {
            entrada.MateriasOrigen = entrada.MateriasDestino
                .SelectMany(d => d.MateriasOrigenPermitidas)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // El cuerpo ya trae la documentación: se respeta lo que envió el estudiante.
        if (entrada.Documentos.Silabos.Count > 0)
            return entrada;

        var documentos = await _documentos.ObtenerPorSolicitudAsync(solicitudId, ct);
        if (documentos.Count == 0)
        {
            throw new AnalisisSolicitudException(400,
                "La solicitud no tiene documentos procesados. Suba la malla, el historial y los sílabos antes de usar el agente externo.");
        }

        if (string.IsNullOrWhiteSpace(entrada.Documentos.MallaOrigen))
            entrada.Documentos.MallaOrigen = UnirTextos(documentos, "MALLA");
        if (string.IsNullOrWhiteSpace(entrada.Documentos.HistorialAcademico))
            entrada.Documentos.HistorialAcademico = UnirTextos(documentos, "HISTORIAL");

        entrada.Documentos.Silabos = usaPreseleccion
            ? entrada.MateriasDestino
                .Select(d => new SilaboAgenteExternoDto
                {
                    Asignatura = d.Nombre,
                    Contenido = string.IsNullOrWhiteSpace(d.Contenido) ? d.Nombre : d.Contenido
                })
                .ToList()
            : ConstruirSilabos(info, documentos);

        return entrada;
    }

    private static MateriaDestinoAgenteExternoDto ConstruirDestino(AsignaturaAnalisisDto entrada) => new()
    {
        AsignaturaId = entrada.AsignaturaDestinoId ?? 0,
        Nombre = entrada.AsignaturaDestino,
        Area = entrada.AreaDestino,
        Nivel = entrada.NivelDestino,
        Contenido = entrada.ContenidoDestino,
        MateriasOrigenPermitidas = entrada.MateriasOrigenPermitidas
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList(),
        NotasMateriasOrigen = entrada.NotasMateriasOrigen
    };

    /// <summary>
    /// Asocia cada asignatura de la solicitud con el texto de un sílabo procesado. Si no hay
    /// sílabos, usa la descripción real de la asignatura para que el agente tenga contenido.
    /// </summary>
    private static List<SilaboAgenteExternoDto> ConstruirSilabos(
        SolicitudCompletaDto info, List<DocumentoExtraidoDto> documentos)
    {
        var silabos = documentos
            .Where(d => EsTipo(d.TipoDocumento, "SILABO") && !string.IsNullOrWhiteSpace(d.TextoExtraido))
            .ToList();

        var usados = new HashSet<int>();
        var lista = new List<SilaboAgenteExternoDto>();

        foreach (var asignatura in info.Asignaturas)
        {
            var documento = silabos.FirstOrDefault(d =>
                !usados.Contains(d.Id) && CoincideConAsignatura(d, asignatura.AsignaturaOrigen));
            documento ??= silabos.FirstOrDefault(d => !usados.Contains(d.Id));

            if (documento is not null)
                usados.Add(documento.Id);

            var contenido = documento?.TextoExtraido;
            lista.Add(new SilaboAgenteExternoDto
            {
                Asignatura = asignatura.AsignaturaOrigen,
                Contenido = string.IsNullOrWhiteSpace(contenido)
                    ? asignatura.ContenidoOrigen
                    : contenido.Trim()
            });
        }

        return lista;
    }

    private static bool EsTipo(string tipo, string esperado) =>
        tipo.Contains(esperado, StringComparison.OrdinalIgnoreCase);

    private static bool CoincideConAsignatura(DocumentoExtraidoDto documento, string asignatura) =>
        (!string.IsNullOrWhiteSpace(documento.NombreArchivo) &&
         documento.NombreArchivo.Contains(asignatura, StringComparison.OrdinalIgnoreCase)) ||
        (!string.IsNullOrWhiteSpace(documento.TextoExtraido) &&
         documento.TextoExtraido.Contains(asignatura, StringComparison.OrdinalIgnoreCase));

    private static string UnirTextos(List<DocumentoExtraidoDto> documentos, string tipo) =>
        string.Join("\n\n", documentos
            .Where(d => EsTipo(d.TipoDocumento, tipo) && !string.IsNullOrWhiteSpace(d.TextoExtraido))
            .Select(d => d.TextoExtraido!.Trim()));

    /// <summary>
    /// La decisión y la justificación se guardan tal como las dio el agente (sin umbral ni reglas).
    /// Solo si faltan se marca la asignatura para revisión manual. Se guardan una o dos materias
    /// de origen (HU-22) con su nota cuando la preselección la conoce.
    /// </summary>
    private static AnalisisAsignatura ConstruirFila(AsignaturaAgenteExternoDto a, AgenteExternoRequest entrada)
    {
        var destino = BuscarDestino(entrada.MateriasDestino, a);
        var sinDecision = string.IsNullOrWhiteSpace(a.Decision);
        var sinJustificacion = string.IsNullOrWhiteSpace(a.Justificacion);

        var origenes = (a.MateriasOrigen ?? new List<string>())
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => m.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (origenes.Count == 0 && !string.IsNullOrWhiteSpace(a.NombreOrigen))
            origenes.Add(a.NombreOrigen.Trim());

        var excedeCombinacion = origenes.Count > 2;
        if (excedeCombinacion)
            origenes = origenes.Take(2).ToList();

        if (origenes.Count == 0)
            origenes.Add("Sin materia de origen");

        var notas = destino?.NotasMateriasOrigen ?? new Dictionary<string, decimal?>();

        var materias = origenes.Select(nombre => new MateriaOrigenResultado
        {
            Nombre = Recortar(nombre, MaxNombre),
            Nota = notas.TryGetValue(nombre, out var nota) ? nota : null
        }).ToList();

        string decision;
        string justificacion;
        bool requiereRevision;

        var motivoExclusion = ReglasHomologacion.MotivoSiNoHomologable(
            string.Join(" + ", origenes), a.NombreDestino);
        if (motivoExclusion is not null)
        {
            // La regla institucional de materias no homologables también aplica al agente externo.
            decision = DecisionAnalisis.NO_HOMOLOGABLE;
            justificacion = motivoExclusion;
            requiereRevision = false;
        }
        else if (excedeCombinacion)
        {
            decision = DecisionAnalisis.REQUIERE_REVISION;
            justificacion = "El agente externo propuso más de dos materias de origen. Requiere revisión manual.";
            requiereRevision = true;
        }
        else if (sinDecision)
        {
            decision = DecisionAnalisis.REQUIERE_REVISION;
            justificacion = sinJustificacion
                ? "El agente externo no entregó decisión ni justificación."
                : Recortar(a.Justificacion.Trim(), MaxJustificacion);
            requiereRevision = true;
        }
        else
        {
            decision = Recortar(a.Decision.Trim(), MaxDecision);
            justificacion = sinJustificacion
                ? "El agente externo no entregó justificación."
                : Recortar(a.Justificacion.Trim(), MaxJustificacion);
            requiereRevision = sinJustificacion;
        }

        return new AnalisisAsignatura
        {
            AsignaturaOrigen = Recortar(string.Join(" + ", origenes), MaxNombre),
            AsignaturaDestino = Recortar(a.NombreDestino.Trim(), MaxNombre),
            AsignaturaDestinoId = a.AsignaturaDestinoId ?? (destino?.AsignaturaId > 0 ? destino.AsignaturaId : null),
            AreaDestino = string.IsNullOrWhiteSpace(destino?.Area) ? null : Recortar(destino!.Area!.Trim(), 80),
            Combinada = materias.Count == 2,
            MateriasOrigen = materias,
            PorcentajeContenido = a.PorcentajeSimilitud,
            PorcentajeProfundidad = 0,
            PorcentajeHoras = 0,
            Decision = decision,
            Justificacion = Recortar(justificacion, MaxJustificacion),
            RequiereRevision = requiereRevision
        };
    }

    private static MateriaDestinoAgenteExternoDto? BuscarDestino(
        List<MateriaDestinoAgenteExternoDto>? destinos, AsignaturaAgenteExternoDto asignatura)
    {
        if (destinos is null || destinos.Count == 0)
            return null;

        if (asignatura.AsignaturaDestinoId is > 0)
        {
            var porId = destinos.FirstOrDefault(d => d.AsignaturaId == asignatura.AsignaturaDestinoId.Value);
            if (porId is not null)
                return porId;
        }

        return destinos.FirstOrDefault(d =>
            string.Equals(d.Nombre?.Trim(), asignatura.NombreDestino?.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static string Recortar(string texto, int max) =>
        texto.Length <= max ? texto : texto[..max];
}
