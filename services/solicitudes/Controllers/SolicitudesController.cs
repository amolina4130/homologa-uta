using HomologaUTA.Solicitudes.Data;
using HomologaUTA.Solicitudes.DTOs;
using HomologaUTA.Solicitudes.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HomologaUTA.Solicitudes.Controllers;

[ApiController]
[Route("solicitudes")]
[Authorize] // Exige que el usuario esté logueado para usar estos endpoints
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
        // 1. Validaciones
        if (string.IsNullOrWhiteSpace(request.Tipo) || request.CarreraOrigenId == 0 || request.CarreraDestinoId == 0)
            return BadRequest(new { error = "Faltan campos obligatorios." });

        if (request.CarreraOrigenId == request.CarreraDestinoId)
            return BadRequest(new { error = "La carrera de origen y destino no pueden ser iguales." });

        if (request.AsignaturasIds == null || !request.AsignaturasIds.Any())
            return BadRequest(new { error = "Debe seleccionar al menos una asignatura." });

        // 2. Obtener el correo desde el token JWT
        var correo = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
        if (string.IsNullOrEmpty(correo))
            return Unauthorized(new { error = "Token no válido." });

        // 3. Crear la solicitud
        var solicitud = new Solicitud
        {
            Tipo = request.Tipo,
            CarreraOrigenId = request.CarreraOrigenId,
            CarreraDestinoId = request.CarreraDestinoId,
            EstudianteCorreo = correo,
            Estado = EstadoSolicitud.REGISTRADA
        };

        // 4. Agregar las asignaturas
        foreach (var asigId in request.AsignaturasIds)
        {
            solicitud.Asignaturas.Add(new SolicitudAsignatura { AsignaturaId = asigId });
        }

        // 5. Guardar en la base de datos
        _db.Solicitudes.Add(solicitud);
        await _db.SaveChangesAsync();

        return Created($"/solicitudes/{solicitud.Id}", new { id = solicitud.Id, mensaje = "Solicitud registrada exitosamente." });
    }

    [HttpGet("mias")]
    public async Task<IActionResult> ObtenerMisSolicitudes()
    {
        // 1. Obtener el correo desde el token JWT
        var correo = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
        if (string.IsNullOrEmpty(correo))
            return Unauthorized(new { error = "Token no válido." });

        // 2. Buscar solo las solicitudes de este estudiante
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
}