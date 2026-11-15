using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Models;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Homologacion.Services;

/// <summary>La resolución no se puede registrar; trae el código HTTP que corresponde.</summary>
public class ResolucionException : Exception
{
    public int StatusCode { get; }
    public ResolucionException(int statusCode, string mensaje) : base(mensaje) => StatusCode = statusCode;
}

/// <summary>Registra la resolución del Consejo Directivo sobre una solicitud con informe técnico (HU-17).</summary>
public class ResolucionService
{
    private readonly HomologacionDbContext _db;
    private readonly SolicitudesClient _solicitudes;
    private readonly ILogger<ResolucionService> _logger;

    public ResolucionService(HomologacionDbContext db, SolicitudesClient solicitudes, ILogger<ResolucionService> logger)
    {
        _db = db;
        _solicitudes = solicitudes;
        _logger = logger;
    }

    public async Task<ResolucionResponse> RegistrarAsync(
        int solicitudId, ResolucionRequest request, string usuario, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.NumeroResolucion))
            throw new ResolucionException(400, "El número de resolución es obligatorio.");
        if (request.Decisiones.Count == 0)
            throw new ResolucionException(400, "Debe registrar la decisión final de al menos una asignatura.");

        var analisis = await _db.AnalisisSolicitudes
            .AsNoTracking()
            .Include(a => a.Asignaturas)
            .FirstOrDefaultAsync(a => a.SolicitudId == solicitudId, ct);

        if (analisis is null || analisis.Estado != EstadoAnalisis.INFORME_EMITIDO)
            throw new ResolucionException(409, "La solicitud no tiene un informe técnico emitido.");

        var existe = await _db.ResolucionesConsejo.AnyAsync(r => r.SolicitudId == solicitudId, ct);
        if (existe)
            throw new ResolucionException(409, "La resolución de esta solicitud ya fue registrada.");

        var idsValidos = analisis.Asignaturas.Select(a => a.Id).ToHashSet();
        var invalida = request.Decisiones.FirstOrDefault(d => !idsValidos.Contains(d.AsignaturaId));
        if (invalida is not null)
            throw new ResolucionException(400, $"La asignatura {invalida.AsignaturaId} no pertenece al informe técnico.");
        if (request.Decisiones.Any(d => string.IsNullOrWhiteSpace(d.DecisionFinal)))
            throw new ResolucionException(400, "Cada asignatura debe tener una decisión final.");

        var resolucion = new ResolucionConsejo
        {
            SolicitudId = solicitudId,
            NumeroResolucion = request.NumeroResolucion.Trim(),
            Fecha = request.Fecha == default ? DateTime.UtcNow : request.Fecha,
            Observacion = request.Observacion,
            FechaRegistro = DateTime.UtcNow,
            Decisiones = request.Decisiones.Select(d => new ResolucionDecision
            {
                AsignaturaId = d.AsignaturaId,
                DecisionFinal = d.DecisionFinal.Trim().ToUpperInvariant()
            }).ToList()
        };

        _db.ResolucionesConsejo.Add(resolucion);
        await _db.SaveChangesAsync(ct);

        try
        {
            await _solicitudes.CambiarEstadoAsync(solicitudId, "RESUELTA", usuario, ct);
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            _logger.LogError(ex, "Resolución {ResolucionId} guardada, pero no se pudo cambiar el estado de la solicitud {SolicitudId}",
                resolucion.Id, solicitudId);
            throw new ResolucionException(502, "La resolución se guardó, pero no se pudo actualizar el estado de la solicitud.");
        }

        _logger.LogInformation("Resolución {Numero} registrada para la solicitud {SolicitudId} por {Usuario}",
            resolucion.NumeroResolucion, solicitudId, usuario);

        return ADto(resolucion);
    }

    public async Task<ResolucionResponse?> ObtenerAsync(int solicitudId, CancellationToken ct = default)
    {
        var resolucion = await _db.ResolucionesConsejo
            .AsNoTracking()
            .Include(r => r.Decisiones)
            .FirstOrDefaultAsync(r => r.SolicitudId == solicitudId, ct);
        return resolucion is null ? null : ADto(resolucion);
    }

    private static ResolucionResponse ADto(ResolucionConsejo r) => new()
    {
        Id = r.Id,
        SolicitudId = r.SolicitudId,
        NumeroResolucion = r.NumeroResolucion,
        Fecha = r.Fecha,
        Observacion = r.Observacion,
        FechaRegistro = r.FechaRegistro,
        Decisiones = r.Decisiones.OrderBy(d => d.Id).Select(d => new ResolucionDecisionResponse
        {
            AsignaturaId = d.AsignaturaId,
            DecisionFinal = d.DecisionFinal
        }).ToList()
    };
}
