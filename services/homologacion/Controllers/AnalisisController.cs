using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace HomologaUTA.Homologacion.Controllers;

[ApiController]
[Route("homologacion")]
// El Gateway ya valida el token (ruta "homologacion", política "autenticado").
public class AnalisisController : ControllerBase
{
    private readonly AnalisisService _servicio;

    public AnalisisController(AnalisisService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>Analiza las asignaturas con el agente, guarda el resultado y deja la solicitud en PENDIENTE_REVISION</summary>
    /// <remarks>
    /// Si el cuerpo viene vacío, se usan las asignaturas reales de la solicitud guardada en Solicitudes.
    /// Por cada asignatura el agente devuelve los porcentajes de contenido, profundidad y carga horaria.
    /// La decisión (HOMOLOGABLE o NO_HOMOLOGABLE) la calcula el código con el umbral del 80 %.
    /// Si el agente no responde bien tras los reintentos, la asignatura queda en REQUIERE_REVISION.
    /// Al terminar se guarda todo, la solicitud pasa a PENDIENTE_REVISION y se publica el evento AnalisisCompletado.
    /// </remarks>
    /// <param name="id">Id de la solicitud</param>
    /// <param name="request">Asignaturas opcionales; si no vienen, se toman de la solicitud</param>
    /// <response code="200">Resultado del análisis por asignatura</response>
    /// <response code="400">Datos de entrada inválidos</response>
    /// <response code="404">La solicitud no existe</response>
    /// <response code="502">El servicio de Solicitudes no responde</response>
    [HttpPost("{id:int}/analizar")]
    [ProducesResponseType(typeof(ResultadoAnalisisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ResultadoAnalisisDto>> Analizar(
        int id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AnalizarSolicitudRequest? request,
        CancellationToken ct)
    {
        if (id <= 0)
            return BadRequest(new { error = "El id de la solicitud debe ser mayor que cero." });

        try
        {
            var resultado = await _servicio.AnalizarSolicitudAsync(id, request?.Asignaturas, ct);
            return Ok(resultado);
        }
        catch (AnalisisSolicitudException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }

    /// <summary>Resultado del análisis que ve el estudiante (propuesta del agente y, si existe, informe del Coordinador)</summary>
    /// <param name="id">Id de la solicitud</param>
    /// <response code="200">Resultado del análisis</response>
    /// <response code="404">La solicitud no existe o todavía no tiene análisis</response>
    /// <response code="502">El servicio de Solicitudes no responde</response>
    [HttpGet("{id:int}/resultado")]
    [ProducesResponseType(typeof(ResultadoSolicitudDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ResultadoSolicitudDto>> Resultado(int id, CancellationToken ct)
    {
        if (id <= 0)
            return BadRequest(new { error = "El id de la solicitud debe ser mayor que cero." });

        try
        {
            return Ok(await _servicio.ObtenerResultadoSolicitudAsync(id, ct));
        }
        catch (AnalisisSolicitudException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }
}
