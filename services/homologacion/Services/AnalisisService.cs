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
/// </summary>
public class AnalisisService
{
    private const int MaxJustificacion = 2000;
    private const int MaxNombre = 200;

    private readonly HomologacionDbContext _db;
    private readonly AgenteInterno _agente;
    private readonly DecisionHomologacion _decision;
    private readonly IAnalisisEventPublisher _publisher;
    private readonly SolicitudesClient _solicitudes;
    private readonly ILogger<AnalisisService> _logger;

    public AnalisisService(
        HomologacionDbContext db,
        AgenteInterno agente,
        DecisionHomologacion decision,
        IAnalisisEventPublisher publisher,
        SolicitudesClient solicitudes,
        ILogger<AnalisisService> logger)
    {
        _db = db;
        _agente = agente;
        _decision = decision;
        _publisher = publisher;
        _solicitudes = solicitudes;
        _logger = logger;
    }

    /// <summary>
    /// Analiza la solicitud. Si no se envían asignaturas, las toma del servicio de Solicitudes
    /// (asignaturas reales seleccionadas por el estudiante).
    /// </summary>
    public async Task<ResultadoAnalisisDto> AnalizarSolicitudAsync(
        int solicitudId,
        IReadOnlyList<AsignaturaAnalisisDto>? asignaturas = null,
        CancellationToken ct = default)
    {
        var info = await _solicitudes.ObtenerCompletaAsync(solicitudId, ct);
        if (info is null)
            throw new AnalisisSolicitudException(404, "Solicitud no encontrada.");

        var entradas = asignaturas is { Count: > 0 }
            ? asignaturas
            : ConstruirAsignaturas(info);

        if (entradas.Count == 0)
            throw new AnalisisSolicitudException(400, "La solicitud no tiene asignaturas para analizar.");

        // 1. Analizar cada asignatura (agente + decisión calculada por código)
        var filas = new List<AnalisisAsignatura>();
        foreach (var a in entradas)
        {
            var resultadoAgente = await _agente.AnalizarAsignaturaAsync(
                a.AsignaturaOrigen, a.ContenidoOrigen, a.AsignaturaDestino, a.ContenidoDestino);

            filas.Add(ConstruirFila(a, resultadoAgente));
        }

        // 2. Guardar: si la solicitud ya se había analizado, se reemplaza su resultado
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
            .FirstOrDefaultAsync(a => a.SolicitudId == solicitudId, ct);

        if (analisis is null)
            throw new AnalisisSolicitudException(404, "La solicitud todavía no tiene un análisis generado.");

        return new ResultadoSolicitudDto
        {
            SolicitudId = solicitudId,
            Numero = $"SOL-{solicitudId:D4}",
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
            ContenidoDestino = a.ContenidoDestino
        }).ToList();

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

        return new AnalisisAsignatura
        {
            AsignaturaOrigen = Recortar(entrada.AsignaturaOrigen, MaxNombre),
            AsignaturaDestino = Recortar(entrada.AsignaturaDestino, MaxNombre),
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
}

/// <summary>Estados del servicio de Solicitudes que usa Homologación.</summary>
internal static class EstadoSolicitudHomologacion
{
    public const string PENDIENTE_REVISION = "PENDIENTE_REVISION";
    public const string INFORME_EMITIDO = "INFORME_EMITIDO";
}
