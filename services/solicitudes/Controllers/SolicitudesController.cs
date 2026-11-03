using HomologaUTA.Solicitudes.Data;
using HomologaUTA.Solicitudes.DTOs;
using HomologaUTA.Solicitudes.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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
        // 1. Validaciones (Ya no validamos el Tipo, porque es fijo)
        if (request.CarreraOrigenId == 0 || request.CarreraDestinoId == 0)
            return BadRequest(new { error = "Faltan campos obligatorios." });

        if (request.CarreraOrigenId == request.CarreraDestinoId)
            return BadRequest(new { error = "La carrera de origen y destino no pueden ser iguales." });

        if (request.AsignaturasIds == null || !request.AsignaturasIds.Any())
            return BadRequest(new { error = "Debe seleccionar al menos una asignatura." });

        var correo = User.FindFirstValue(ClaimTypes.Email)
                     ?? User.FindFirstValue("email")
                     ?? "estudiante@uta.edu.ec"; // Fallback para pruebas

        // 2. El tipo se fuerza a "Homologación"
        var solicitud = new Solicitud
        {
            Tipo = "Homologación",
            CarreraOrigenId = request.CarreraOrigenId,
            CarreraDestinoId = request.CarreraDestinoId,
            EstudianteCorreo = correo,
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
                     ?? "estudiante@uta.edu.ec"; // Fallback

        var solicitudes = await _db.Solicitudes
            .Include(s => s.CarreraOrigen)
            .Include(s => s.CarreraDestino)
            .Where(s => s.EstudianteCorreo == correo)
            .OrderByDescending(s => s.FechaRegistro)
            .Select(s => new SolicitudResponse
            {
                Id = s.Id,
                Tipo = s.Tipo,
                CarreraOrigen = s.CarreraOrigen.Nombre,
                CarreraDestino = s.CarreraDestino.Nombre,
                FechaRegistro = s.FechaRegistro.ToString("yyyy-MM-dd"),
                Estado = s.Estado
            })
            .ToListAsync();

        return Ok(solicitudes);
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
}