using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.Connectors;
using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Models;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Homologacion.Services;

/// <summary>El informe no se puede emitir; trae el código HTTP que corresponde.</summary>
public class InformeException : Exception
{
    public int StatusCode { get; }

    public InformeException(int statusCode, string mensaje) : base(mensaje)
    {
        StatusCode = statusCode;
    }
}

/// <summary>Lista las solicitudes pendientes, muestra su detalle con la propuesta del agente y guarda el informe técnico.</summary>
public class CoordinadorService
{
    private readonly HomologacionDbContext _db;
    private readonly SolicitudesClient _solicitudes;
    private readonly AnalisisService _analisis;
    private readonly AnalisisExternoService _analisisExterno;
    private readonly ILogger<CoordinadorService> _logger;

    public CoordinadorService(
        HomologacionDbContext db,
        SolicitudesClient solicitudes,
        AnalisisService analisis,
        AnalisisExternoService analisisExterno,
        ILogger<CoordinadorService> logger)
    {
        _db = db;
        _solicitudes = solicitudes;
        _analisis = analisis;
        _analisisExterno = analisisExterno;
        _logger = logger;
    }

    /// <summary>Solicitudes pendientes de revisión, las más antiguas primero.</summary>
    public async Task<List<PendienteDto>> ListarPendientesAsync(CancellationToken ct = default)
    {
        // Fuente 1: análisis que ya están esperando al Coordinador.
        var analisis = await _db.AnalisisSolicitudes
            .AsNoTracking()
            .Where(a => a.Estado == EstadoAnalisis.PENDIENTE_REVISION)
            .Select(a => new
            {
                a.SolicitudId,
                a.FechaAnalisis,
                TotalAsignaturas = a.Asignaturas.Count
            })
            .ToListAsync(ct);

        // Fuente 2: solicitudes marcadas como pendientes. Así también aparecen las que
        // quedaron sin análisis por un fallo previo (al abrirlas se genera).
        var pendientes = await _solicitudes.ObtenerPendientesAsync(ct);

        var ids = analisis.Select(a => a.SolicitudId)
            .Union(pendientes.Select(p => p.Id))
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return new List<PendienteDto>();

        var infos = await _solicitudes.ObtenerPorIdsAsync(ids, ct);
        var infoPorId = infos.ToDictionary(i => i.Id);
        var analisisPorId = analisis.ToDictionary(a => a.SolicitudId);

        var lista = new List<PendienteDto>();
        foreach (var id in ids)
        {
            if (!infoPorId.TryGetValue(id, out var info))
            {
                _logger.LogWarning("La solicitud {SolicitudId} está pendiente pero no existe en Solicitudes", id);
                continue;
            }

            analisisPorId.TryGetValue(id, out var a);

            lista.Add(new PendienteDto
            {
                SolicitudId = id,
                Fecha = info.FechaRegistro,
                Estudiante = info.EstudianteCorreo,
                Tipo = info.Tipo,
                CarreraOrigen = info.CarreraOrigen,
                CarreraDestino = info.CarreraDestino,
                FechaAnalisis = a?.FechaAnalisis ?? info.FechaRegistro,
                TotalAsignaturas = a?.TotalAsignaturas ?? info.TotalAsignaturas
            });
        }

        return lista.OrderBy(p => p.Fecha).ToList();
    }

    /// <summary>
    /// Detalle de una solicitud con la propuesta real del agente por asignatura. Si la
    /// solicitud existe pero aún no tiene análisis, se genera en ese momento con sus
    /// asignaturas reales; nunca se devuelven datos inventados.
    /// </summary>
    public async Task<DetalleSolicitudDto?> ObtenerDetalleAsync(int solicitudId, CancellationToken ct = default)
    {
        var info = await _solicitudes.ObtenerCompletaAsync(solicitudId, ct);
        if (info is null)
            return null;

        var analisis = await CargarAnalisisAsync(solicitudId, ct);

        if (analisis is null)
        {
            // Una solicitud con informe emitido pero sin análisis es un dato inconsistente.
            if (string.Equals(info.Estado, EstadoSolicitudHomologacion.INFORME_EMITIDO, StringComparison.OrdinalIgnoreCase))
                return null;

            try
            {
                if (string.Equals(info.Modulo, "EXTERNO", StringComparison.OrdinalIgnoreCase))
                {
                    await _analisisExterno.AnalizarSolicitudAsync(solicitudId, null, ct);
                }
                else
                {
                    await _analisis.AnalizarSolicitudAsync(
                        solicitudId, AnalisisService.ConstruirAsignaturas(info), ct);
                }
            }
            catch (Exception ex) when (ex is AnalisisSolicitudException
                                       or AgenteExternoTimeoutException
                                       or AgenteExternoException
                                       or DocumentosNoDisponibleException
                                       or SolicitudesNoDisponibleException)
            {
                _logger.LogWarning(ex, "No se pudo generar el análisis de la solicitud {SolicitudId}", solicitudId);
                return null;
            }

            analisis = await CargarAnalisisAsync(solicitudId, ct);
        }

        if (analisis is null)
            return null;

        return new DetalleSolicitudDto
        {
            SolicitudId = solicitudId,
            Estado = analisis.Estado,
            Fecha = info.FechaRegistro,
            Estudiante = info.EstudianteCorreo,
            Tipo = info.Tipo,
            CarreraOrigen = info.CarreraOrigen,
            CarreraDestino = info.CarreraDestino,
            Modulo = info.Modulo,
            UniversidadOrigen = info.UniversidadOrigen,
            UniversidadDestino = info.UniversidadDestino,
            FechaAnalisis = analisis.FechaAnalisis,
            CoordinadorCorreo = analisis.CoordinadorCorreo,
            FechaInforme = analisis.FechaInforme,
            Asignaturas = analisis.Asignaturas
                .OrderBy(a => a.Id)
                .Select(a => new DetalleAsignaturaDto
                {
                    Id = a.Id,
                    AsignaturaOrigen = a.AsignaturaOrigen,
                    AsignaturaDestino = a.AsignaturaDestino,
                    Propuesta = new PropuestaAgenteDto
                    {
                        Decision = a.Decision,
                        Justificacion = a.Justificacion,
                        PorcentajeContenido = a.PorcentajeContenido,
                        PorcentajeProfundidad = a.PorcentajeProfundidad,
                        PorcentajeHoras = a.PorcentajeHoras,
                        RequiereRevision = a.RequiereRevision
                    },
                    DecisionCoordinador = a.DecisionCoordinador,
                    ObservacionCoordinador = a.ObservacionCoordinador
                })
                .ToList()
        };
    }

    /// <exception cref="InformeException">404 sin análisis/solicitud, 409 si ya se emitió, 400 si el informe no es válido.</exception>
    public async Task<ResultadoInformeDto> EmitirInformeAsync(
        int solicitudId, EmitirInformeRequest request, string coordinadorCorreo, CancellationToken ct = default)
    {
        var info = await _solicitudes.ObtenerCompletaAsync(solicitudId, ct);
        if (info is null)
            throw new InformeException(404, "Solicitud no encontrada.");

        var analisis = await _db.AnalisisSolicitudes
            .Include(a => a.Asignaturas)
            .FirstOrDefaultAsync(a => a.SolicitudId == solicitudId, ct);

        if (analisis is null)
            throw new InformeException(404, "La solicitud no tiene un análisis para revisar.");

        if (analisis.Estado == EstadoAnalisis.INFORME_EMITIDO)
            throw new InformeException(409, "El informe técnico de esta solicitud ya fue emitido.");

        ValidarInforme(analisis, request);

        foreach (var item in request.Asignaturas)
        {
            var asignatura = analisis.Asignaturas.First(a => a.Id == item.AsignaturaId);
            asignatura.DecisionCoordinador = NormalizarDecision(item.Decision);
            asignatura.ObservacionCoordinador = item.Observacion.Trim();
        }

        analisis.Estado = EstadoAnalisis.INFORME_EMITIDO;
        analisis.CoordinadorCorreo = coordinadorCorreo;
        analisis.FechaInforme = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // El informe ya quedó guardado. Si Solicitudes no responde, se registra el problema
        // pero no se pierde el trabajo del Coordinador.
        var estadoActualizado = false;
        try
        {
            estadoActualizado = await _solicitudes.CambiarEstadoAsync(
                solicitudId, EstadoSolicitudHomologacion.INFORME_EMITIDO, coordinadorCorreo, ct);
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            _logger.LogError(ex, "El informe de {SolicitudId} se guardó, pero no se pudo actualizar Solicitudes", solicitudId);
        }

        return new ResultadoInformeDto
        {
            SolicitudId = solicitudId,
            Estado = analisis.Estado,
            CoordinadorCorreo = coordinadorCorreo,
            FechaInforme = analisis.FechaInforme.Value,
            EstadoSolicitudActualizado = estadoActualizado,
            Asignaturas = analisis.Asignaturas
                .OrderBy(a => a.Id)
                .Select(a => new InformeAsignaturaResultadoDto
                {
                    AsignaturaId = a.Id,
                    AsignaturaOrigen = a.AsignaturaOrigen,
                    Decision = a.DecisionCoordinador!,
                    Observacion = a.ObservacionCoordinador!
                })
                .ToList()
        };
    }

    private async Task<AnalisisSolicitud?> CargarAnalisisAsync(int solicitudId, CancellationToken ct) =>
        await _db.AnalisisSolicitudes
            .Include(a => a.Asignaturas)
            .FirstOrDefaultAsync(a => a.SolicitudId == solicitudId, ct);

    /// <summary>Valida que el informe traiga exactamente las asignaturas del análisis, sin repetidas ni ajenas.</summary>
    private static void ValidarInforme(AnalisisSolicitud analisis, EmitirInformeRequest request)
    {
        if (request?.Asignaturas is null || request.Asignaturas.Count == 0)
            throw new InformeException(400, "Debe enviar la decisión de al menos una asignatura.");

        var idsAnalisis = analisis.Asignaturas.Select(a => a.Id).ToHashSet();
        var vistos = new HashSet<int>();

        foreach (var item in request.Asignaturas)
        {
            if (!idsAnalisis.Contains(item.AsignaturaId))
                throw new InformeException(400, $"La asignatura {item.AsignaturaId} no pertenece a esta solicitud.");

            if (!vistos.Add(item.AsignaturaId))
                throw new InformeException(400, $"La asignatura {item.AsignaturaId} está repetida en el informe.");

            var decision = NormalizarDecision(item.Decision);
            if (decision != DecisionAnalisis.HOMOLOGABLE && decision != DecisionAnalisis.NO_HOMOLOGABLE)
                throw new InformeException(400, $"Decisión inválida para la asignatura {item.AsignaturaId}. Use HOMOLOGABLE o NO_HOMOLOGABLE.");

            if (string.IsNullOrWhiteSpace(item.Observacion))
                throw new InformeException(400, $"La observación de la asignatura {item.AsignaturaId} es obligatoria.");

            if (item.Observacion.Trim().Length > 1000)
                throw new InformeException(400, $"La observación de la asignatura {item.AsignaturaId} supera los 1000 caracteres.");
        }

        if (vistos.Count != idsAnalisis.Count)
            throw new InformeException(400, "Debe enviar la decisión de todas las asignaturas del análisis.");
    }

    private static string NormalizarDecision(string? decision) =>
        (decision ?? string.Empty).Trim().ToUpperInvariant();
}
