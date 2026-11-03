using HomologaUTA.Homologacion.Clients;
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
    private const int LoteSolicitudes = 200;

    private readonly HomologacionDbContext _db;
    private readonly SolicitudesClient _solicitudes;
    private readonly ILogger<CoordinadorService> _logger;

    public CoordinadorService(HomologacionDbContext db, SolicitudesClient solicitudes, ILogger<CoordinadorService> logger)
    {
        _db = db;
        _solicitudes = solicitudes;
        _logger = logger;
    }

    /// <summary>Solicitudes en PENDIENTE_REVISION, las más antiguas primero.</summary>
    public async Task<List<PendienteDto>> ListarPendientesAsync(CancellationToken ct = default)
    {
        var pendientes = await _db.AnalisisSolicitudes
            .Where(a => a.Estado == EstadoAnalisis.PENDIENTE_REVISION)
            .OrderBy(a => a.FechaAnalisis)
            .Select(a => new { a.SolicitudId, a.FechaAnalisis, Total = a.Asignaturas.Count })
            .ToListAsync(ct);

        // Fecha, estudiante, tipo y carreras viven en el servicio de Solicitudes
        var info = new Dictionary<int, SolicitudInfoDto>();
        foreach (var lote in pendientes.Select(p => p.SolicitudId).Chunk(LoteSolicitudes))
        {
            foreach (var s in await _solicitudes.ObtenerPorIdsAsync(lote, ct))
                info[s.Id] = s;
        }

        var lista = new List<PendienteDto>();
        foreach (var p in pendientes)
        {
            if (!info.TryGetValue(p.SolicitudId, out var s))
            {
                _logger.LogWarning("La solicitud {SolicitudId} tiene análisis pero no existe en Solicitudes", p.SolicitudId);
                continue;
            }

            lista.Add(new PendienteDto
            {
                SolicitudId = p.SolicitudId,
                Fecha = s.FechaRegistro,
                Estudiante = s.EstudianteCorreo,
                Tipo = s.Tipo,
                CarreraOrigen = s.CarreraOrigen,
                CarreraDestino = s.CarreraDestino,
                FechaAnalisis = p.FechaAnalisis,
                TotalAsignaturas = p.Total
            });
        }

        return lista;
    }

    /// <summary>Detalle de una solicitud con la propuesta del agente por asignatura; null si no tiene análisis.</summary>
    public async Task<DetalleSolicitudDto?> ObtenerDetalleAsync(int solicitudId, CancellationToken ct = default)
    {
        var analisis = await _db.AnalisisSolicitudes
            .AsNoTracking()
            .Include(a => a.Asignaturas)
            .FirstOrDefaultAsync(a => a.SolicitudId == solicitudId, ct);

        if (analisis is null)
            return null;

        var info = (await _solicitudes.ObtenerPorIdsAsync(new[] { solicitudId }, ct)).FirstOrDefault();
        if (info is null)
            _logger.LogWarning("La solicitud {SolicitudId} tiene análisis pero no existe en Solicitudes", solicitudId);

        return new DetalleSolicitudDto
        {
            SolicitudId = solicitudId,
            Estado = analisis.Estado,
            Fecha = info?.FechaRegistro ?? analisis.FechaAnalisis,
            Estudiante = info?.EstudianteCorreo ?? string.Empty,
            Tipo = info?.Tipo ?? string.Empty,
            CarreraOrigen = info?.CarreraOrigen ?? string.Empty,
            CarreraDestino = info?.CarreraDestino ?? string.Empty,
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

    /// <exception cref="InformeException">404 sin análisis, 409 si ya se emitió, 400 si el informe no es válido.</exception>
    public async Task<ResultadoInformeDto> EmitirInformeAsync(
        int solicitudId, EmitirInformeRequest request, string coordinadorCorreo, CancellationToken ct = default)
    {
        var analisis = await _db.AnalisisSolicitudes
            .Include(a => a.Asignaturas)
            .FirstOrDefaultAsync(a => a.SolicitudId == solicitudId, ct)
            ?? throw new InformeException(404, "La solicitud no tiene un análisis para revisar.");

        if (analisis.Estado != EstadoAnalisis.PENDIENTE_REVISION)
            throw new InformeException(409, "El informe técnico de esta solicitud ya fue emitido.");

        // Debe haber una decisión por cada asignatura del análisis: ni más ni menos
        var enviados = request.Asignaturas.Select(a => a.AsignaturaId).ToList();
        if (enviados.Distinct().Count() != enviados.Count)
            throw new InformeException(400, "No repita asignaturas en el informe.");

        var esperados = analisis.Asignaturas.Select(a => a.Id).ToHashSet();
        var faltan = esperados.Except(enviados).ToList();
        var sobran = enviados.Except(esperados).ToList();

        if (faltan.Count > 0)
            throw new InformeException(400, $"Falta la decisión de las asignaturas: {string.Join(", ", faltan)}.");
        if (sobran.Count > 0)
            throw new InformeException(400, $"Estas asignaturas no pertenecen a la solicitud: {string.Join(", ", sobran)}.");

        // Decisiones válidas
        var decisiones = new Dictionary<int, (string Decision, string Observacion)>();
        foreach (var item in request.Asignaturas)
        {
            var decision = item.Decision.Trim().ToUpperInvariant();
            if (decision != DecisionAnalisis.HOMOLOGABLE && decision != DecisionAnalisis.NO_HOMOLOGABLE)
                throw new InformeException(400,
                    $"Decisión inválida en la asignatura {item.AsignaturaId}: use HOMOLOGABLE o NO_HOMOLOGABLE.");

            decisiones[item.AsignaturaId] = (decision, item.Observacion.Trim());
        }

        // Guardar
        var fecha = DateTime.UtcNow;
        foreach (var asignatura in analisis.Asignaturas)
        {
            var (decision, observacion) = decisiones[asignatura.Id];
            asignatura.DecisionCoordinador = decision;
            asignatura.ObservacionCoordinador = observacion;
        }

        analisis.Estado = EstadoAnalisis.INFORME_EMITIDO;
        analisis.CoordinadorCorreo = coordinadorCorreo;
        analisis.FechaInforme = fecha;

        await _db.SaveChangesAsync(ct);

        return new ResultadoInformeDto
        {
            SolicitudId = solicitudId,
            Estado = analisis.Estado,
            CoordinadorCorreo = coordinadorCorreo,
            FechaInforme = fecha,
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
}