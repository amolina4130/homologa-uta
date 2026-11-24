using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomologaUTA.Homologacion.Controllers;

/// <summary>Resolución del Consejo Directivo sobre una solicitud con informe emitido (HU-17).</summary>
[ApiController]
[Route("homologacion")]
public class ResolucionController : ControllerBase
{
    private readonly ResolucionService _servicio;
    private readonly SolicitudesClient _solicitudes;

    public ResolucionController(ResolucionService servicio, SolicitudesClient solicitudes)
    {
        _servicio = servicio;
        _solicitudes = solicitudes;
    }

    /// <summary>Registra la resolución del Consejo y deja la solicitud en RESUELTA.</summary>
    /// <remarks>Solo se registra una vez y sobre una solicitud con informe técnico emitido.</remarks>
    /// <response code="201">Resolución registrada</response>
    /// <response code="403">El usuario no es Coordinador ni Consejo</response>
    /// <response code="409">No hay informe emitido o ya existe la resolución</response>
    [HttpPost("{id:int}/resolucion")]
    [ProducesResponseType(typeof(ResolucionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Registrar(
        int id, [FromBody] RegistrarResolucionRequest request, CancellationToken ct)
    {
        if (id <= 0)
            return BadRequest(new { mensaje = "El id de la solicitud debe ser mayor que cero." });

        if (!AutorizacionHomologacion.PuedeRegistrarResolucion(User))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { mensaje = "Solo el Coordinador puede registrar la resolución." });

        var usuario = AutorizacionHomologacion.Correo(User) ?? "consejo@homologauta";

        try
        {
            var resolucion = await _servicio.RegistrarAsync(id, request, usuario, ct);
            return CreatedAtAction(nameof(Obtener), new { id }, resolucion);
        }
        catch (ResolucionException ex)
        {
            return StatusCode(ex.StatusCode, new { mensaje = ex.Message });
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { mensaje = ex.Message });
        }
    }

    /// <summary>Consulta la resolución registrada para una solicitud.</summary>
    /// <response code="200">Resolución registrada</response>
    /// <response code="404">La solicitud todavía no tiene resolución</response>
    [HttpGet("{id:int}/resolucion")]
    [ProducesResponseType(typeof(ResolucionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(int id, CancellationToken ct)
    {
        if (id <= 0)
            return BadRequest(new { mensaje = "El id de la solicitud debe ser mayor que cero." });

        try
        {
            var info = await _solicitudes.ObtenerCompletaAsync(id, ct);
            if (info is null)
                return NotFound(new { mensaje = "Solicitud no encontrada." });

            if (!AutorizacionHomologacion.PuedeAccederSolicitud(User, info.EstudianteCorreo))
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { mensaje = "No puede consultar la resolución de otro estudiante." });

            var resolucion = await _servicio.ObtenerAsync(id, ct);
            return resolucion is null
                ? NotFound(new { mensaje = "La solicitud todavía no tiene una resolución del Consejo." })
                : Ok(resolucion);
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { mensaje = ex.Message });
        }
    }
}
