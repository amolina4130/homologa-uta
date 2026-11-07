using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.Connectors;
using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Eventos;
using HomologaUTA.Homologacion.Models;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Homologacion.Services;

/// <summary>
/// Envía la documentación al agente externo y guarda su decisión por asignatura con su justificación,
/// sin imponerle reglas. Si el agente falla, no se guarda nada: se puede reintentar sin perder datos.
/// </summary>
public class AnalisisExternoService
{
    private const int MaxJustificacion = 2000;
    private const int MaxNombre = 200;
    private const int MaxDecision = 30;

    private readonly HomologacionDbContext _db;
    private readonly AgenteExternoConnector _conector;
    private readonly SolicitudesClient _solicitudes;
    private readonly DocumentosClient _documentos;
    private readonly IAnalisisEventPublisher _publisher;
    private readonly ILogger<AnalisisExternoService> _logger;

    public AnalisisExternoService(
        HomologacionDbContext db,
        AgenteExternoConnector conector,
        SolicitudesClient solicitudes,
        DocumentosClient documentos,
        IAnalisisEventPublisher publisher,
        ILogger<AnalisisExternoService> logger)
    {
        _db = db;
        _conector = conector;
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
    /// <exception cref="AgenteExternoTimeoutException">Vencíó el tiempo de espera; no se guardó nada.</exception>
    /// <exception cref="AgenteExternoException">El agente falló o respondió mal; no se guardó nada.</exception>
    public async Task<ResultadoAnalisisDto> AnalizarSolicitudAsync(
        int solicitudId, AgenteExternoRequest? request, CancellationToken ct = default)
    {
        var info = await _solicitudes.ObtenerCompletaAsync(solicitudId, ct)
                   ?? throw new AnalisisSolicitudException(404, "Solicitud no encontrada.");

        var entrada = await PrepararEntradaAsync(solicitudId, info, request, ct);

        // 1. Enviar al agente. Si falla o vence el tiempo, sale por excepción antes de tocar la base de datos.
        var respuesta = await _conector.EnviarDocumentacionAsync(entrada, ct);
        var filas = respuesta.Asignaturas.Select(ConstruirFila).ToList();

        // 2. Guardar: si la solicitud ya tenía un análisis, se reemplaza
        var fecha = DateTime.UtcNow;
        var analisis = await _db.AnalisisSolicitudes
            .Include(x => x.Asignaturas)
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
    /// Completa la petición del agente con los datos reales de la solicitud. Si no vienen sílabos
    /// en el cuerpo, se toman de los documentos procesados (texto extraído de los PDF).
    /// </summary>
    private async Task<AgenteExternoRequest> PrepararEntradaAsync(
        int solicitudId, SolicitudCompletaDto info, AgenteExternoRequest? request, CancellationToken ct)
    {
        var entrada = request ?? new AgenteExternoRequest();
        entrada.Documentos ??= new DocumentosAgenteExternoDto();
        entrada.SolicitudId = solicitudId;

        if (string.IsNullOrWhiteSpace(entrada.Estudiante))
            entrada.Estudiante = info.EstudianteCorreo;
        if (string.IsNullOrWhiteSpace(entrada.CarreraOrigen))
            entrada.CarreraOrigen = info.CarreraOrigen;
        if (string.IsNullOrWhiteSpace(entrada.CarreraDestino))
            entrada.CarreraDestino = info.CarreraDestino;

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

        entrada.Documentos.Silabos = ConstruirSilabos(info, documentos);
        return entrada;
    }

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
    /// Solo si faltan se marca la asignatura para revisión manual.
    /// </summary>
    private static AnalisisAsignatura ConstruirFila(AsignaturaAgenteExternoDto a)
    {
        var sinDecision = string.IsNullOrWhiteSpace(a.Decision);
        var sinJustificacion = string.IsNullOrWhiteSpace(a.Justificacion);

        return new AnalisisAsignatura
        {
            AsignaturaOrigen = Recortar(a.NombreOrigen.Trim(), MaxNombre),
            AsignaturaDestino = Recortar(a.NombreDestino.Trim(), MaxNombre),
            PorcentajeContenido = a.PorcentajeSimilitud,
            PorcentajeProfundidad = 0,
            PorcentajeHoras = 0,
            Decision = sinDecision
                ? DecisionAnalisis.REQUIERE_REVISION
                : Recortar(a.Decision.Trim(), MaxDecision),
            Justificacion = sinJustificacion
                ? "El agente externo no entregó justificación."
                : Recortar(a.Justificacion.Trim(), MaxJustificacion),
            RequiereRevision = sinDecision || sinJustificacion
        };
    }

    private static string Recortar(string texto, int max) =>
        texto.Length <= max ? texto : texto[..max];
}
