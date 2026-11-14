using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomologaUTA.Homologacion.Controllers;

/// <summary>Extracción de las materias aprobadas del récord ya subido (HU-20).</summary>
[ApiController]
[Route("homologacion")]
public class RecordController : ControllerBase
{
    private readonly ExtractorRecordService _servicio;
    private readonly SolicitudesClient _solicitudes;

    public RecordController(ExtractorRecordService servicio, SolicitudesClient solicitudes)
    {
        _servicio = servicio;
        _solicitudes = solicitudes;
    }

    /// <summary>
    /// Reutiliza el documento de récord académico ya procesado, extrae sus materias y notas y
    /// las guarda para la solicitud. Si el PDF no es legible, responde 200 con requiereRevision.
    /// </summary>
    /// <response code="200">Materias extraídas o aviso de revisión manual</response>
    /// <response code="404">La solicitud no tiene documento de récord</response>
    /// <response code="422">No se pudo leer el documento</response>
    [HttpPost("{id:int}/record/extraer")]
    [ProducesResponseType(typeof(ResultadoExtraccionRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Extraer(int id, CancellationToken ct)
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
                    new { mensaje = "No puede extraer el récord de otro estudiante." });

            return Ok(await _servicio.ExtraerAsync(id, ct));
        }
        catch (RecordException ex)
        {
            return StatusCode(ex.StatusCode, new { mensaje = ex.Message });
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { mensaje = ex.Message });
        }
    }
}
