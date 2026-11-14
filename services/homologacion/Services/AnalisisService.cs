using HomologaUTA.Homologacion.Agents;
using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Eventos;
using HomologaUTA.Homologacion.Models;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Homologacion.Services;

/// <summary>El análisis no se puede generar o consultar; trae el código HTTP que corresponde.</summary>
public class AnalisisSolicitudException : Exception
{
    public int StatusCode { get; }

    public AnalisisSolicitudException(int statusCode, string mensaje) : base(mensaje)
    {
        StatusCode = statusCode;
    }
}

/// <summary>
/// Analiza las asignaturas de una solicitud con el agente interno, guarda la decisión, los porcentajes
/// y la justificación por asignatura, deja la solicitud en PENDIENTE_REVISION y publica AnalisisCompletado.
/// Cuando el estudiante tiene materias aprobadas extraídas del récord, antes de llamar al agente se
/// hace la preselección por área y nivel (HU-21) y se permite la combinación de dos materias (HU-22).
/// </summary>
public class AnalisisService
{
    private const int MaxJustificacion = 2000;
    private const int MaxNombre = 200;

    private readonly HomologacionDbContext _db;
    private readonly AgenteInterno _agente;
    private readonly DecisionHomologacion _decision;
    private readonly PreseleccionService _preseleccion;
    private readonly IAnalisisEventPublisher _publisher;
    private readonly SolicitudesClient _solicitudes;
    private readonly ILogger<AnalisisService> _logger;

    public AnalisisService(
        HomologacionDbContext db,
        AgenteInterno agente,
        DecisionHomologacion decision,
        PreseleccionService preseleccion,
        IAnalisisEventPublisher publisher,
        SolicitudesClient solicitudes,
        ILogger<AnalisisService> logger)
    {
        _db = db;
        _agente = agente;
        _decision = decision;
        _preseleccion = preseleccion;
        _publisher = publisher;
        _solicitudes = solicitudes;
        _logger = logger;
    }

    /// <summary>
    /// Analiza la solicitud. Si no se envían asignaturas, las toma del servicio de Solicitudes
    /// (asignaturas reales seleccionadas por el estudiante) o de la preselección si ya existen
    /// materias aprobadas del récord.
    /// </summary>
    public async Task<ResultadoAnalisisDto> AnalizarSolicitudAsync(
        int solicitudId,
        IReadOnlyList<AsignaturaAnalisisDto>? asignaturas = null,
        CancellationToken ct = default)
    {
        var info = await _solicitudes.ObtenerCompletaAsync(solicitudId, ct);
        if (info is null)
            throw new AnalisisSolicitudException(404, "Solicitud no encontrada.");

        EntradasPreseleccion? preseleccion = null;
        IReadOnlyList<AsignaturaAnalisisDto> entradas;

        if (asignaturas is { Count: > 0 })
        {
            entradas = asignaturas;
        }
        else
        {
            try
            {
                preseleccion = await _preseleccion.ConstruirEntradasAsync(solicitudId, ct);
            }
            catch (MemoriaNoDisponibleException ex)
            {
                throw new AnalisisSolicitudException(502, ex.Message);
            }

            entradas = preseleccion.UsaMateriasAprobadas
                ? preseleccion.Entradas
                : ConstruirAsignaturas(info);
        }

        var sinCandidata = preseleccion?.SinCandidata ?? new List<MateriaSinCandidataDto>();

        if (entradas.Count == 0 && sinCandidata.Count == 0)
            throw new AnalisisSolicitudException(400, "La solicitud no tiene asignaturas para analizar.");

        // 1. Analizar cada asignatura candidata (agente + decisión calculada por código).
        // Las que no tienen candidata no se envían al agente: quedan en revisión con el motivo.
        var filas = new List<AnalisisAsignatura>();
        foreach (var a in entradas)
        {
            var resultadoAgente = await AnalizarConAgenteAsync(a);
            filas.Add(ConstruirFila(a, resultadoAgente));
        }

        foreach (var materia in sinCandidata)
        {
            filas.Add(new AnalisisAsignatura
            {
                AsignaturaOrigen = Recortar(materia.MateriaOrigen, MaxNombre),
                AsignaturaDestino = "Sin equivalente probable",
                Decision = DecisionAnalisis.REQUIERE_REVISION,
                Justificacion = Recortar(
                    string.IsNullOrWhiteSpace(materia.Motivo)
                        ? "No hay una asignatura de destino candidata para esta materia."
                        : materia.Motivo,
                    MaxJustificacion),
                RequiereRevision = true
            });
        }

        // 2. Guardar: si la solicitud ya se había analizado, se reemplaza su resultado
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

        // 3. Publicar el evento solo después de guardar. Si RabbitMQ falla, el análisis ya quedó guardado.
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

        // 4. Mover la solicitud a PENDIENTE_REVISION para que aparezca al Coordinador.
        var estadoActualizado = false;
        try
        {
            estadoActualizado = await _solicitudes.CambiarEstadoAsync(
                solicitudId, EstadoSolicitudHomologacion.PENDIENTE_REVISION, "sistema@homologauta", ct);
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            _logger.LogError(ex, "El análisis de {SolicitudId} se guardó, pero no se pudo actualizar su estado", solicitudId);
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

    /// <summary>Resultado que ve el estudiante con la propuesta del agente y, si existe, el informe del Coordinador.</summary>
    public async Task<ResultadoSolicitudDto> ObtenerResultadoSolicitudAsync(
        int solicitudId, CancellationToken ct = default)
    {
        var info = await _solicitudes.ObtenerCompletaAsync(solicitudId, ct);
        if (info is null)
            throw new AnalisisSolicitudException(404, "Solicitud no encontrada.");

        var analisis = await _db.AnalisisSolicitudes
            .AsNoTracking()
            .Include(a => a.Asignaturas)
                .ThenInclude(a => a.MateriasOrigen)
            .FirstOrDefaultAsync(a => a.SolicitudId == solicitudId, ct);

        if (analisis is null)
            throw new AnalisisSolicitudException(404, "La solicitud todavía no tiene un análisis generado.");

        return new ResultadoSolicitudDto
        {
            SolicitudId = solicitudId,
            Numero = string.IsNullOrWhiteSpace(info.NumeroProceso)
                ? $"SOL-{solicitudId:D4}"
                : info.NumeroProceso,
            Tipo = info.Tipo,
            Modulo = string.Equals(info.Modulo, "EXTERNO", StringComparison.OrdinalIgnoreCase)
                ? "Agente externo"
                : "Agente interno",
            Estado = analisis.Estado,
            Estudiante = info.EstudianteCorreo,
            FechaRegistro = info.FechaRegistro,
            CarreraOrigen = info.CarreraOrigen,
            CarreraDestino = info.CarreraDestino,
            ModuloSolicitud = info.Modulo,
            UniversidadOrigen = info.UniversidadOrigen,
            UniversidadDestino = info.UniversidadDestino,
            FechaAnalisis = analisis.FechaAnalisis,
            FechaInforme = analisis.FechaInforme,
            CoordinadorCorreo = analisis.CoordinadorCorreo,
            Asignaturas = analisis.Asignaturas
                .OrderBy(a => a.Id)
                .Select(a => new AsignaturaResultadoSolicitudDto
                {
                    Id = a.Id,
                    AsignaturaOrigen = a.AsignaturaOrigen,
                    AsignaturaDestino = a.AsignaturaDestino,
                    AsignaturaDestinoId = a.AsignaturaDestinoId,
                    Combinada = a.Combinada,
                    MateriasOrigen = a.MateriasOrigen
                        .Select(m => new MateriaOrigenDto { Nombre = m.Nombre, Nota = m.Nota })
                        .ToList(),
                    AreaOrigen = a.AreaOrigen,
                    AreaDestino = a.AreaDestino,
                    PorcentajeContenido = a.PorcentajeContenido,
                    PorcentajeProfundidad = a.PorcentajeProfundidad,
                    PorcentajeHoras = a.PorcentajeHoras,
                    Decision = a.Decision,
                    Justificacion = a.Justificacion,
                    RequiereRevision = a.RequiereRevision,
                    DecisionCoordinador = a.DecisionCoordinador,
                    ObservacionCoordinador = a.ObservacionCoordinador
                })
                .ToList()
        };
    }

    /// <summary>Convierte las asignaturas reales de la solicitud en la entrada del agente.</summary>
    public static List<AsignaturaAnalisisDto> ConstruirAsignaturas(SolicitudCompletaDto info) =>
        info.Asignaturas.Select(a => new AsignaturaAnalisisDto
        {
            AsignaturaOrigen = a.AsignaturaOrigen,
            ContenidoOrigen = a.ContenidoOrigen,
            AsignaturaDestino = a.AsignaturaDestino,
            ContenidoDestino = a.ContenidoDestino,
            AsignaturaDestinoId = a.AsignaturaDestinoId,
            AreaOrigen = a.AreaOrigen,
            AreaDestino = a.AreaDestino,
            NivelOrigen = a.NivelOrigen,
            NivelDestino = a.NivelDestino,
            EquivalenciaAplicada = a.EquivalenciaAplicada,
            MateriasOrigenPermitidas = new List<string> { a.AsignaturaOrigen }
        }).ToList();

    /// <summary>
    /// Aplica una equivalencia registrada antes de llamar al agente: si la regla existe, la
    /// asignatura queda homologable y se cita la regla. En otro caso, el agente compara contenidos.
    /// </summary>
    private async Task<ResultadoAgente> AnalizarConAgenteAsync(AsignaturaAnalisisDto entrada)
    {
        if (!string.IsNullOrWhiteSpace(entrada.EquivalenciaAplicada))
        {
            return new ResultadoAgente
            {
                Contenido = 100,
                Profundidad = 100,
                Horas = 100,
                Justificacion = entrada.EquivalenciaAplicada.Trim(),
                RequiereRevision = false,
                MateriasOrigen = new List<string> { entrada.AsignaturaOrigen }
            };
        }

        return await _agente.AnalizarAsignaturaAsync(
            entrada.AsignaturaOrigen,
            entrada.ContenidoOrigen,
            entrada.AsignaturaDestino,
            entrada.ContenidoDestino,
            entrada.MateriasOrigenPermitidas.Count > 0 ? entrada.MateriasOrigenPermitidas : null);
    }

    private AnalisisAsignatura ConstruirFila(AsignaturaAnalisisDto entrada, ResultadoAgente agente)
    {
        string decision;
        string justificacion;

        if (agente.RequiereRevision)
        {
            // El agente no respondió bien tras los reintentos: lo decide una persona
            decision = DecisionAnalisis.REQUIERE_REVISION;
            justificacion = agente.Justificacion;
        }
        else
        {
            // La decisión la calcula el código (umbral del 80 %), no el agente
            var d = _decision.CalcularDecision(agente.Contenido, agente.Profundidad, agente.Horas);
            decision = d.Decision;
            justificacion = string.IsNullOrWhiteSpace(agente.Justificacion)
                ? d.Justificacion
                : $"{d.Justificacion} Análisis del agente: {agente.Justificacion.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(entrada.EquivalenciaAplicada) &&
            !justificacion.Contains(entrada.EquivalenciaAplicada, StringComparison.OrdinalIgnoreCase))
        {
            justificacion = $"{entrada.EquivalenciaAplicada.Trim()} {justificacion}";
        }

        var nombres = (agente.MateriasOrigen ?? new List<string>())
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => m.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (nombres.Count == 0)
            nombres.Add(entrada.AsignaturaOrigen.Trim());

        if (nombres.Count > 2)
            throw new AnalisisSolicitudException(400,
                "Una asignatura de destino no puede cubrirse con más de dos materias de origen.");

        if (entrada.MateriasOrigenPermitidas.Count > 0)
        {
            var ajenas = nombres
                .Where(n => !entrada.MateriasOrigenPermitidas.Contains(n, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (ajenas.Count > 0)
                throw new AnalisisSolicitudException(400,
                    $"El agente propuso materias que no pertenecen al estudiante: {string.Join(", ", ajenas)}.");
        }

        var materias = nombres.Select((nombre, indice) => new MateriaOrigenResultado
        {
            Nombre = Recortar(nombre, MaxNombre),
            Nota = entrada.NotasMateriasOrigen.TryGetValue(nombre, out var nota)
                ? nota
                : indice == 0 ? entrada.NotaOrigen : null
        }).ToList();

        return new AnalisisAsignatura
        {
            AsignaturaOrigen = Recortar(string.Join(" + ", nombres), MaxNombre),
            AsignaturaDestino = Recortar(entrada.AsignaturaDestino, MaxNombre),
            AsignaturaDestinoId = entrada.AsignaturaDestinoId,
            AreaOrigen = RecortarOpcional(entrada.AreaOrigen, 80),
            AreaDestino = RecortarOpcional(entrada.AreaDestino, 80),
            Combinada = nombres.Count == 2,
            MateriasOrigen = materias,
            PorcentajeContenido = agente.Contenido,
            PorcentajeProfundidad = agente.Profundidad,
            PorcentajeHoras = agente.Horas,
            Decision = decision,
            Justificacion = Recortar(justificacion, MaxJustificacion),
            RequiereRevision = agente.RequiereRevision
        };
    }

    private static string Recortar(string texto, int max) =>
        texto.Length <= max ? texto : texto[..max];

    private static string? RecortarOpcional(string? texto, int max) =>
        string.IsNullOrWhiteSpace(texto)
            ? null
            : texto.Trim().Length <= max ? texto.Trim() : texto.Trim()[..max];
}

/// <summary>Estados del servicio de Solicitudes que usa Homologación.</summary>
internal static class EstadoSolicitudHomologacion
{
    public const string PENDIENTE_REVISION = "PENDIENTE_REVISION";
    public const string INFORME_EMITIDO = "INFORME_EMITIDO";
}
