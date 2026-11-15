using HomologaUTA.Solicitudes.Data;
using HomologaUTA.Solicitudes.DTOs;
using HomologaUTA.Solicitudes.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Net.Http.Json;

namespace HomologaUTA.Solicitudes.Controllers;

[ApiController]
[Route("solicitudes")]
// [Authorize] <--- Lo dejamos comentado para tus pruebas
public class SolicitudesController : ControllerBase
{
    private readonly SolicitudesDbContext _db;

    public SolicitudesController(SolicitudesDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> CrearSolicitud([FromBody] SolicitudRequest request)
    {
        if (request.CarreraOrigenId == 0 || request.CarreraDestinoId == 0)
            return BadRequest(new { error = "Faltan campos obligatorios." });

        if (request.CarreraOrigenId == request.CarreraDestinoId)
            return BadRequest(new { error = "La carrera de origen y destino no pueden ser iguales." });

        if (request.AsignaturasIds == null || !request.AsignaturasIds.Any())
            return BadRequest(new { error = "Debe seleccionar al menos una asignatura." });

        var modulo = string.Equals(request.Modulo?.Trim(), "EXTERNO", StringComparison.OrdinalIgnoreCase)
            ? "EXTERNO"
            : "INTERNO";

        if (modulo == "EXTERNO" &&
            (string.IsNullOrWhiteSpace(request.UniversidadOrigen) ||
             string.IsNullOrWhiteSpace(request.UniversidadDestino)))
        {
            return BadRequest(new
            {
                error = "Para el agente externo debe indicar la universidad de origen y la universidad de destino."
            });
        }

        var correo = User.FindFirstValue(ClaimTypes.Email) 
                     ?? User.FindFirstValue("email")
                     ?? "estudiante@uta.edu.ec";

        var solicitud = new Solicitud
        {
            Tipo = "Homologación",
            CarreraOrigenId = request.CarreraOrigenId,
            CarreraDestinoId = request.CarreraDestinoId,
            EstudianteCorreo = correo,
            Modulo = modulo,
            UniversidadOrigen = request.UniversidadOrigen?.Trim(),
            UniversidadDestino = request.UniversidadDestino?.Trim(),
            // La solicitud nace REGISTRADA. Homologación la pasa a PENDIENTE_REVISION
            // cuando el agente termina el análisis (así el Coordinador nunca ve una
            // solicitud sin propuesta).
            Estado = EstadoSolicitud.REGISTRADA
        };

        foreach (var asigId in request.AsignaturasIds)
        {
            solicitud.Asignaturas.Add(new SolicitudAsignatura { AsignaturaId = asigId });
        }

        _db.Solicitudes.Add(solicitud);

        _db.HistorialEstados.Add(new HistorialEstado
        {
            Solicitud = solicitud,
            EstadoAnterior = null,
            EstadoNuevo = solicitud.Estado,
            UsuarioCorreo = correo,
            FechaCambio = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        
        return Created($"/solicitudes/{solicitud.Id}", new { id = solicitud.Id, mensaje = "Solicitud registrada exitosamente." });
    }

    [HttpGet("mias")]
    public async Task<IActionResult> ObtenerMisSolicitudes()
    {
        var correo = User.FindFirstValue(ClaimTypes.Email) 
                     ?? User.FindFirstValue("email")
                     ?? "estudiante@uta.edu.ec";

        var solicitudes = await _db.Solicitudes
            .Include(s => s.CarreraOrigen)
            .Include(s => s.CarreraDestino)
            .Where(s => s.EstudianteCorreo == correo)
            .OrderByDescending(s => s.FechaRegistro)
            .Select(s => new 
            {
                Id = s.Id,
                Numero = "SOL-" + s.Id.ToString("D4"),
                Tipo = s.Tipo,
                Origen = s.CarreraOrigen != null ? s.CarreraOrigen.Nombre : "Origen",
                Destino = s.CarreraDestino != null ? s.CarreraDestino.Nombre : "Destino",
                FechaRegistro = s.FechaRegistro.ToString("yyyy-MM-dd"),
                Estado = s.Estado,
                Modulo = s.Modulo,
                UniversidadOrigen = s.UniversidadOrigen,
                UniversidadDestino = s.UniversidadDestino
            })
            .ToListAsync();

        return Ok(solicitudes);
    }

    /// <summary>Datos de una solicitud propia (incluye el módulo de análisis).</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> ObtenerSolicitud(int id)
    {
        var correo = User.FindFirstValue(ClaimTypes.Email)
                     ?? User.FindFirstValue("email")
                     ?? "estudiante@uta.edu.ec";

        var solicitud = await _db.Solicitudes
            .AsNoTracking()
            .Include(s => s.CarreraOrigen)
            .Include(s => s.CarreraDestino)
            .FirstOrDefaultAsync(s => s.Id == id && s.EstudianteCorreo == correo);

        if (solicitud is null)
            return NotFound(new { error = "Solicitud no encontrada." });

        return Ok(new
        {
            Id = solicitud.Id,
            Numero = "SOL-" + solicitud.Id.ToString("D4"),
            Tipo = solicitud.Tipo,
            Origen = solicitud.CarreraOrigen?.Nombre ?? "Origen",
            Destino = solicitud.CarreraDestino?.Nombre ?? "Destino",
            FechaRegistro = solicitud.FechaRegistro,
            Estado = solicitud.Estado,
            Modulo = solicitud.Modulo,
            UniversidadOrigen = solicitud.UniversidadOrigen,
            UniversidadDestino = solicitud.UniversidadDestino
        });
    }

    [HttpGet("{id:int}/historial")]
    public async Task<IActionResult> ObtenerHistorial(int id)
    {
        var existe = await _db.Solicitudes.AnyAsync(s => s.Id == id);
        if (!existe)
            return NotFound(new { error = "Solicitud no encontrada." });

        var historial = await _db.HistorialEstados
            .Where(h => h.SolicitudId == id)
            .OrderBy(h => h.FechaCambio)
            .Select(h => new
            {
                h.Id,
                h.EstadoAnterior,
                h.EstadoNuevo,
                Usuario = h.UsuarioCorreo,
                Fecha = h.FechaCambio
            })
            .ToListAsync();

        return Ok(historial);
    }

    // --- ENDPOINT PARA EL COORDINADOR (LISTA) ---
    [HttpGet("internal/solicitudes/pendientes")]
    public async Task<IActionResult> ObtenerPendientes()
    {
        var pendientes = await _db.Solicitudes
            .Include(s => s.CarreraOrigen)
            .Include(s => s.CarreraDestino)
            .Where(s => s.Estado == EstadoSolicitud.PENDIENTE_REVISION)
            .OrderBy(s => s.FechaRegistro)
            .Select(s => new 
            {
                Id = s.Id,
                FechaRegistro = s.FechaRegistro,
                EstudianteCorreo = s.EstudianteCorreo,
                Tipo = s.Tipo,
                CarreraOrigen = s.CarreraOrigen != null ? s.CarreraOrigen.Nombre : "Origen",
                CarreraDestino = s.CarreraDestino != null ? s.CarreraDestino.Nombre : "Destino"
            })
            .ToListAsync();

        return Ok(pendientes);
    }

    // --- ENDPOINT PARA BUSCAR POR IDS (DETALLE DEL COORDINADOR) ---
    [HttpGet("internal/solicitudes")]
    public async Task<IActionResult> ObtenerPorIds([FromQuery] string ids)
    {
        if (string.IsNullOrEmpty(ids))
            return Ok(new List<object>());

        var idList = ids.Split(',').Select(int.Parse).ToList();

        var solicitudes = await _db.Solicitudes
            .Include(s => s.CarreraOrigen)
            .Include(s => s.CarreraDestino)
            .Where(s => idList.Contains(s.Id))
            .Select(s => new 
            {
                Id = s.Id,
                FechaRegistro = s.FechaRegistro,
                EstudianteCorreo = s.EstudianteCorreo,
                Tipo = s.Tipo,
                CarreraOrigen = s.CarreraOrigen != null ? s.CarreraOrigen.Nombre : "Origen",
                CarreraDestino = s.CarreraDestino != null ? s.CarreraDestino.Nombre : "Destino",
                Estado = s.Estado
            })
            .ToListAsync();

        return Ok(solicitudes);
    }
}