using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomologaUTA.Homologacion.Controllers;

/// <summary>Preselección de materias candidatas por área y nivel (HU-21).</summary>
[ApiController]
[Route("homologacion")]
public class PreseleccionController : ControllerBase
{
    private readonly PreseleccionService _servicio;
    private readonly SolicitudesClient _solicitudes;

    public PreseleccionController(PreseleccionService servicio, SolicitudesClient solicitudes)
    {
        _servicio = servicio;
        _solicitudes = solicitudes;
    }

    /// <summary>
    /// Devuelve, para cada materia aprobada del récord, las asignaturas de destino candidatas.
    /// Las materias sin candidata se informan en <c>sinCandidata</c> con el motivo.
    /// </summary>
    /// <response code="200">Preselección calculada</response>
    /// <response code="409">No hay materias aprobadas o la memoria del agente no está cargada</response>
    [HttpPost("{id:int}/preseleccion")]
    [ProducesResponseType(typeof(PreseleccionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
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
                    new { mensaje = "No puede consultar la preselección de otro estudiante." });

            return Ok(await _servicio.ObtenerPreseleccionAsync(id, ct));
        }
        catch (AnalisisSolicitudException ex)
        {
            return StatusCode(ex.StatusCode, new { mensaje = ex.Message });
        }
        catch (MemoriaNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { mensaje = ex.Message });
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { mensaje = ex.Message });
        }
    }
}
