using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomologaUTA.Homologacion.Controllers;

/// <summary>Consulta y corrección de las materias aprobadas extraídas del récord (HU-20).</summary>
[ApiController]
[Route("homologacion")]
public class MateriasAprobadasController : ControllerBase
{
    private readonly MateriasAprobadasService _servicio;
    private readonly SolicitudesClient _solicitudes;

    public MateriasAprobadasController(MateriasAprobadasService servicio, SolicitudesClient solicitudes)
    {
        _servicio = servicio;
        _solicitudes = solicitudes;
    }

    /// <summary>Lista las materias aprobadas guardadas para la solicitud.</summary>
    [HttpGet("{id:int}/materias-aprobadas")]
    [ProducesResponseType(typeof(List<MateriaAprobadaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
                    new { mensaje = "No puede consultar las materias aprobadas de otro estudiante." });

            return Ok(await _servicio.ObtenerAsync(id, ct));
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

    /// <summary>
    /// Reemplaza la lista de materias aprobadas. Solo el estudiante dueño y solo mientras la
    /// solicitud siga REGISTRADA; la corrección queda en el historial.
    /// </summary>
    /// <response code="200">Lista corregida</response>
    /// <response code="403">La solicitud no pertenece al estudiante</response>
    /// <response code="409">La solicitud ya pasó a revisión</response>
    [HttpPut("{id:int}/materias-aprobadas")]
    [ProducesResponseType(typeof(List<MateriaAprobadaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Actualizar(
        int id, [FromBody] ActualizarMateriasAprobadasRequest request, CancellationToken ct)
    {
        if (id <= 0)
            return BadRequest(new { mensaje = "El id de la solicitud debe ser mayor que cero." });

        var usuario = AutorizacionHomologacion.Correo(User) ?? "estudiante@homologauta";

        try
        {
            var materias = await _servicio.ActualizarAsync(
                id, request, usuario, AutorizacionHomologacion.EsCoordinadorOAdministrador(User), ct);
            return Ok(materias);
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
