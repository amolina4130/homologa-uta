using System.Security.Claims;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomologaUTA.Homologacion.Controllers;

/// <summary>Registro de la resolución del Consejo Directivo (HU-17).</summary>
[ApiController]
[Route("homologacion")]
public class ResolucionController : ControllerBase
{
    private readonly ResolucionService _servicio;

    public ResolucionController(ResolucionService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>Registra la resolución del Consejo sobre una solicitud con informe técnico emitido</summary>
    /// <response code="201">Resolución registrada y solicitud en estado RESUELTA</response>
    /// <response code="400">Datos inválidos</response>
    /// <response code="403">El usuario no es Coordinador</response>
    /// <response code="409">No hay informe técnico emitido o la resolución ya existe</response>
    [HttpPost("{id:int}/resolucion")]
    [ProducesResponseType(typeof(ResolucionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Registrar(int id, [FromBody] ResolucionRequest request, CancellationToken ct)
    {
        if (User.FindFirst("rol") is { } rol && rol.Value is not ("COORDINADOR" or "ADMINISTRADOR"))
            return StatusCode(StatusCodes.Status403Forbidden, new { mensaje = "Solo el Coordinador puede registrar la resolución." });

        try
        {
            var resultado = await _servicio.RegistrarAsync(id, request, Correo() ?? "coordinador@uta.edu.ec", ct);
            return CreatedAtAction(nameof(Obtener), new { id }, resultado);
        }
        catch (ResolucionException ex)
        {
            return StatusCode(ex.StatusCode, new { mensaje = ex.Message });
        }
    }

    /// <summary>Consulta la resolución registrada de una solicitud</summary>
    [HttpGet("{id:int}/resolucion")]
    [ProducesResponseType(typeof(ResolucionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(int id, CancellationToken ct)
    {
        var resolucion = await _servicio.ObtenerAsync(id, ct);
        return resolucion is null
            ? NotFound(new { mensaje = "La solicitud no tiene resolución registrada." })
            : Ok(resolucion);
    }

    private string? Correo() =>
        User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
}
