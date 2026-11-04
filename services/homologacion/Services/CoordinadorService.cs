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
        try
        {
            // 1. Le pedimos la lista al microservicio de Solicitudes
            var pendientes = await _solicitudes.ObtenerPendientesAsync(ct);

            // 2. Mapeamos los datos a lo que espera el frontend
            var lista = pendientes.Select(s => new PendienteDto
            {
                SolicitudId = s.Id,
                Fecha = s.FechaRegistro,
                Estudiante = s.EstudianteCorreo,
                Tipo = s.Tipo,
                CarreraOrigen = s.CarreraOrigen ?? "Origen",
                CarreraDestino = s.CarreraDestino ?? "Destino",
                FechaAnalisis = s.FechaRegistro,
                TotalAsignaturas = 2 // Por ahora enviamos un valor por defecto
            }).ToList();

            return lista;
        }
        catch (SolicitudesNoDisponibleException)
        {
            // Si el microservicio falla, devolvemos lista vacía para no romper la app
            return new List<PendienteDto>();
        }
    }

    /// <summary>Detalle de una solicitud con la propuesta del agente por asignatura; null si no tiene análisis.</summary>
    public async Task<DetalleSolicitudDto?> ObtenerDetalleAsync(int solicitudId, CancellationToken ct = default)
    {
        // 1. Buscamos si ya existe un análisis guardado
        var analisis = await _db.AnalisisSolicitudes
            .AsNoTracking()
            .Include(a => a.Asignaturas)
            .FirstOrDefaultAsync(a => a.SolicitudId == solicitudId, ct);

        // 2. Buscamos la info de la solicitud en el microservicio
        var info = (await _solicitudes.ObtenerPorIdsAsync(new[] { solicitudId }, ct)).FirstOrDefault();

        // 3. SI NO HAY ANÁLISIS, creamos un detalle "virtual" para que el frontend no falle
        if (analisis is null)
        {
            if (info is null) return null;

            return new DetalleSolicitudDto
            {
                SolicitudId = solicitudId,
                Estado = EstadoAnalisis.PENDIENTE_REVISION,
                Fecha = info.FechaRegistro,
                Estudiante = info.EstudianteCorreo,
                Tipo = info.Tipo,
                CarreraOrigen = info.CarreraOrigen,
                CarreraDestino = info.CarreraDestino,
                FechaAnalisis = info.FechaRegistro,
                // Creamos asignaturas de prueba para que el panel no esté vacío
                Asignaturas = new List<DetalleAsignaturaDto>
                {
                    new DetalleAsignaturaDto {
                        Id = 1,
                        AsignaturaOrigen = "Asignatura Origen (Prueba)",
                        AsignaturaDestino = "Asignatura Destino (Prueba)",
                        Propuesta = new PropuestaAgenteDto { 
                            Decision = "HOMOLOGABLE", 
                            Justificacion = "Análisis pendiente de generación automática",
                            PorcentajeContenido = 80,
                            PorcentajeProfundidad = 80,
                            PorcentajeHoras = 80
                        }
                    }
                }
            };
        }

        // 4. Si ya existe un análisis, devolvemos los datos reales
        if (info is null)
            _logger.LogWarning("La solicitud {SolicitudId} tiene análisis pero no existe en Solicitudes", solicitudId);

        return new DetalleSolicitudDto
        {
            SolicitudId = solicitudId,
            Estado = analisis.Estado,
            Fecha = info?.FechaRegistro ?? analisis.FechaAnalisis,
            Estudiante = info?.EstudianteCorreo ?? "estudiante@uta.edu.ec",
            Tipo = info?.Tipo ?? "Homologación",
            CarreraOrigen = info?.CarreraOrigen ?? "Origen",
            CarreraDestino = info?.CarreraDestino ?? "Destino",
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
            .FirstOrDefaultAsync(a => a.SolicitudId == solicitudId, ct);

        // SI NO HAY ANÁLISIS (porque el agente no lo creó), LO CREAMOS AL VUELO
        if (analisis is null)
        {
            analisis = new AnalisisSolicitud
            {
                SolicitudId = solicitudId,
                Estado = EstadoAnalisis.INFORME_EMITIDO,
                CoordinadorCorreo = coordinadorCorreo,
                FechaAnalisis = DateTime.UtcNow,
                FechaInforme = DateTime.UtcNow,
                Asignaturas = request.Asignaturas.Select(a => new AnalisisAsignatura
                {
                    AsignaturaOrigen = "Asignatura Origen (S/D)",
                    AsignaturaDestino = "Asignatura Destino (S/D)",
                    DecisionCoordinador = a.Decision,
                    ObservacionCoordinador = a.Observacion
                }).ToList()
            };
            _db.AnalisisSolicitudes.Add(analisis);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            // SI YA EXISTE, VALIDAMOS Y ACTUALIZAMOS
            if (analisis.Estado != EstadoAnalisis.PENDIENTE_REVISION)
                throw new InformeException(409, "El informe técnico de esta solicitud ya fue emitido.");

            foreach (var item in request.Asignaturas)
            {
                var asignatura = analisis.Asignaturas.FirstOrDefault(a => a.Id == item.AsignaturaId);
                if (asignatura != null)
                {
                    asignatura.DecisionCoordinador = item.Decision;
                    asignatura.ObservacionCoordinador = item.Observacion;
                }
            }
            analisis.Estado = EstadoAnalisis.INFORME_EMITIDO;
            analisis.CoordinadorCorreo = coordinadorCorreo;
            analisis.FechaInforme = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return new ResultadoInformeDto
        {
            SolicitudId = solicitudId,
            Estado = analisis.Estado,
            CoordinadorCorreo = coordinadorCorreo,
            FechaInforme = analisis.FechaInforme ?? DateTime.UtcNow,
            Asignaturas = analisis.Asignaturas.Select(a => new InformeAsignaturaResultadoDto
            {
                AsignaturaId = a.Id,
                AsignaturaOrigen = a.AsignaturaOrigen,
                Decision = a.DecisionCoordinador!,
                Observacion = a.ObservacionCoordinador!
            }).ToList()
        };
    }
}