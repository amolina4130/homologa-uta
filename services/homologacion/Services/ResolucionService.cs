using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Models;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Homologacion.Services;

/// <summary>La resolución del Consejo no se puede registrar o consultar.</summary>
public class ResolucionException : Exception
{
    public int StatusCode { get; }

    public ResolucionException(int statusCode, string mensaje) : base(mensaje)
    {
        StatusCode = statusCode;
    }
}

/// <summary>
/// Registra la resolución del Consejo Directivo sobre una solicitud con informe técnico emitido
/// (HU-17). Solo deja constancia: el sistema no reemplaza el trámite oficial.
/// </summary>
public class ResolucionService
{
    private readonly HomologacionDbContext _db;
    private readonly SolicitudesClient _solicitudes;
    private readonly ILogger<ResolucionService> _logger;

    public ResolucionService(
        HomologacionDbContext db,
        SolicitudesClient solicitudes,
        ILogger<ResolucionService> logger)
    {
        _db = db;
        _solicitudes = solicitudes;
        _logger = logger;
    }

    public async Task<ResolucionDto> RegistrarAsync(
        int solicitudId,
        RegistrarResolucionRequest request,
        string usuarioCorreo,
        CancellationToken ct = default)
    {
        if (request is null)
            throw new ResolucionException(400, "Los datos de la resolución son obligatorios.");

        if (string.IsNullOrWhiteSpace(request.NumeroResolucion))
            throw new ResolucionException(400, "El número de resolución es obligatorio.");

        if (request.Decisiones is null || request.Decisiones.Count == 0)
            throw new ResolucionException(400, "Debe enviar la decisión final de al menos una asignatura.");

        var info = await _solicitudes.ObtenerCompletaAsync(solicitudId, ct)
                   ?? throw new ResolucionException(404, "Solicitud no encontrada.");

        var analisis = await _db.AnalisisSolicitudes
            .Include(a => a.Asignaturas)
            .Include(a => a.Resolucion!)
                .ThenInclude(r => r.Decisiones)
            .FirstOrDefaultAsync(a => a.SolicitudId == solicitudId, ct);

        if (analisis is null)
            throw new ResolucionException(409, "La solicitud no tiene informe técnico emitido.");

        if (analisis.Estado != EstadoAnalisis.INFORME_EMITIDO)
            throw new ResolucionException(409, "La solicitud no tiene informe técnico emitido.");

        if (analisis.Resolucion is not null)
            throw new ResolucionException(409, "La resolución del Consejo de esta solicitud ya fue registrada.");

        ValidarDecisiones(analisis, request);

        var resolucion = new ResolucionConsejo
        {
            AnalisisSolicitudId = analisis.Id,
            SolicitudId = solicitudId,
            NumeroResolucion = request.NumeroResolucion.Trim(),
            Fecha = request.Fecha == default ? DateTime.UtcNow : request.Fecha,
            Observacion = string.IsNullOrWhiteSpace(request.Observacion)
                ? null
                : request.Observacion.Trim(),
            FechaRegistro = DateTime.UtcNow,
            RegistradoPor = usuarioCorreo
        };

        foreach (var decision in request.Decisiones)
        {
            resolucion.Decisiones.Add(new DecisionResolucion
            {
                AsignaturaId = decision.AsignaturaId,
                DecisionFinal = NormalizarDecision(decision.DecisionFinal)
            });
        }

        _db.ResolucionesConsejo.Add(resolucion);
        analisis.Estado = EstadoAnalisis.RESUELTA;
        await _db.SaveChangesAsync(ct);

        // La solicitud pasa a RESUELTA y el cambio queda en el historial con usuario y fecha.
        try
        {
            await _solicitudes.CambiarEstadoAsync(
                solicitudId, "RESUELTA", usuarioCorreo, ct);
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            _logger.LogError(ex,
                "La resolución de {SolicitudId} se guardó, pero no se pudo cambiar su estado", solicitudId);
        }

        return ConstruirDto(resolucion, analisis.Asignaturas, "RESUELTA");
    }

    public async Task<ResolucionDto?> ObtenerAsync(int solicitudId, CancellationToken ct = default)
    {
        var resolucion = await _db.ResolucionesConsejo
            .AsNoTracking()
            .Include(r => r.Decisiones)
            .Include(r => r.AnalisisSolicitud)
                .ThenInclude(a => a.Asignaturas)
            .FirstOrDefaultAsync(r => r.SolicitudId == solicitudId, ct);

        if (resolucion is null)
            return null;

        return ConstruirDto(
            resolucion,
            resolucion.AnalisisSolicitud.Asignaturas,
            resolucion.AnalisisSolicitud.Estado);
    }

    /// <summary>Carga la resolución (si existe) para el detalle del Coordinador.</summary>
    public async Task<ResolucionConsejo?> CargarAsync(int solicitudId, CancellationToken ct = default) =>
        await _db.ResolucionesConsejo
            .AsNoTracking()
            .Include(r => r.Decisiones)
            .FirstOrDefaultAsync(r => r.SolicitudId == solicitudId, ct);

    public static ResolucionDto ConstruirDto(
        ResolucionConsejo resolucion,
        IEnumerable<AnalisisAsignatura> asignaturas,
        string estadoSolicitud)
    {
        var porId = asignaturas.ToDictionary(a => a.Id);

        return new ResolucionDto
        {
            Id = resolucion.Id,
            SolicitudId = resolucion.SolicitudId,
            NumeroResolucion = resolucion.NumeroResolucion,
            Fecha = resolucion.Fecha,
            Observacion = resolucion.Observacion,
            FechaRegistro = resolucion.FechaRegistro,
            RegistradoPor = resolucion.RegistradoPor,
            EstadoSolicitud = estadoSolicitud,
            Decisiones = resolucion.Decisiones
                .OrderBy(d => d.Id)
                .Select(d =>
                {
                    porId.TryGetValue(d.AsignaturaId, out var asignatura);
                    return new DecisionResolucionDto
                    {
                        AsignaturaId = d.AsignaturaId,
                        AsignaturaOrigen = asignatura?.AsignaturaOrigen ?? string.Empty,
                        AsignaturaDestino = asignatura?.AsignaturaDestino ?? string.Empty,
                        DecisionFinal = d.DecisionFinal,
                        Combinada = asignatura?.Combinada ?? false,
                        MateriasOrigen = asignatura?.MateriasOrigen
                            .Select(m => m.Nombre)
                            .ToList() ?? new List<string>()
                    };
                })
                .ToList()
        };
    }

    private static void ValidarDecisiones(AnalisisSolicitud analisis, RegistrarResolucionRequest request)
    {
        var idsAnalisis = analisis.Asignaturas.Select(a => a.Id).ToHashSet();
        var vistos = new HashSet<int>();

        foreach (var decision in request.Decisiones)
        {
            if (!idsAnalisis.Contains(decision.AsignaturaId))
                throw new ResolucionException(400,
                    $"La asignatura {decision.AsignaturaId} no pertenece a esta solicitud.");

            if (!vistos.Add(decision.AsignaturaId))
                throw new ResolucionException(400,
                    $"La asignatura {decision.AsignaturaId} está repetida en la resolución.");

            var valor = NormalizarDecision(decision.DecisionFinal);
            if (valor != DecisionAnalisis.HOMOLOGABLE && valor != DecisionAnalisis.NO_HOMOLOGABLE)
                throw new ResolucionException(400,
                    $"Decisión inválida para la asignatura {decision.AsignaturaId}. Use HOMOLOGABLE o NO_HOMOLOGABLE.");
        }

        if (vistos.Count != idsAnalisis.Count)
            throw new ResolucionException(400,
                "Debe enviar la decisión final de todas las asignaturas del informe.");
    }

    private static string NormalizarDecision(string? decision) =>
        (decision ?? string.Empty).Trim().ToUpperInvariant();
}
