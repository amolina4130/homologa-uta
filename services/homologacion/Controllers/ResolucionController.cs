using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.Models;
using HomologaUTA.Homologacion.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HomologaUTA.Homologacion.Controllers;

[ApiController]
[Route("homologacion")]
public class ResolucionController : ControllerBase
{
    private readonly HomologacionDbContext _db;
    private readonly SolicitudesClient _solicitudes;

    public ResolucionController(HomologacionDbContext db, SolicitudesClient solicitudes)
    {
        _db = db;
        _solicitudes = solicitudes;
    }

    public class DecisionRequest
    {
        public int AsignaturaId { get; set; }
        public string DecisionFinal { get; set; } = string.Empty;
    }

    public class ResolucionRequest
    {
        public string NumeroResolucion { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public List<DecisionRequest> Decisiones { get; set; } = new();
        public string? Observacion { get; set; }
    }

    [HttpPost("{id}/resolucion")]
    [Authorize(Roles = "COORDINADOR")]
    public async Task<IActionResult> RegistrarResolucion(int id, [FromBody] ResolucionRequest request)
    {
        // 1. Verificar que el análisis exista y esté en estado INFORME_EMITIDO
        var analisis = await _db.AnalisisSolicitudes
            .Include(a => a.Asignaturas)
            .FirstOrDefaultAsync(a => a.SolicitudId == id);

        if (analisis is null)
            return NotFound(new { mensaje = "La solicitud no tiene un análisis generado." });

        if (analisis.Estado != EstadoAnalisis.INFORME_EMITIDO)
            return Conflict(new { mensaje = "La solicitud no tiene un informe técnico emitido o ya fue resuelta." });

        // 2. Verificar que no exista ya una resolución para esta solicitud
        var existeResolucion = await _db.ResolucionesConsejo.AnyAsync(r => r.SolicitudId == id);
        if (existeResolucion)
            return Conflict(new { mensaje = "La resolución del Consejo ya fue registrada para esta solicitud." });

        // 3. Obtener el usuario actual (Coordinador)
        var correoUsuario = User.FindFirstValue(ClaimTypes.Email) ?? "coordinador@uta.edu.ec";

        // 4. Crear la resolución
        var resolucion = new ResolucionConsejo
        {
            SolicitudId = id,
            NumeroResolucion = request.NumeroResolucion,
            Fecha = request.Fecha,
            Observacion = request.Observacion,
            UsuarioRegistro = correoUsuario,
            Decisiones = request.Decisiones.Select(d => new DecisionAsignaturaResolucion
            {
                AsignaturaId = d.AsignaturaId,
                DecisionFinal = d.DecisionFinal
            }).ToList()
        };

        _db.ResolucionesConsejo.Add(resolucion);

        // 5. Cambiar el estado del análisis a RESUELTA
        analisis.Estado = EstadoAnalisis.RESUELTA; // Asegúrate de tener este estado en tu enum/clase
        await _db.SaveChangesAsync();

        // 6. Actualizar el estado de la solicitud en el servicio de Solicitudes
        try
        {
            await _solicitudes.CambiarEstadoAsync(id, "RESUELTA", correoUsuario);
        }
        catch (Exception ex)
        {
            // Si falla el servicio de solicitudes, la resolución ya está guardada. Se podría reintentar o loggear.
            Console.WriteLine($"Error al actualizar estado en Solicitudes: {ex.Message}");
        }

        return CreatedAtAction(nameof(RegistrarResolucion), new { id = resolucion.Id }, new { mensaje = "Resolución registrada exitosamente.", resolucionId = resolucion.Id });
    }
}