using HomologaUTA.Homologacion.Connectors;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomologaUTA.Homologacion.Controllers;

[ApiController]
[Route("homologacion")]
// El Gateway ya valida el token (ruta "homologacion", política "autenticado").
public class AnalisisExternoController : ControllerBase
{
    private readonly AnalisisExternoService _servicio;

    public AnalisisExternoController(AnalisisExternoService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>Envía la documentación al agente externo y guarda su decisión justificada por asignatura</summary>
    /// <remarks>
    /// El agente externo decide por su cuenta: no se le envían reglas ni se le aplica ningún umbral.
    /// Si vence el tiempo de espera (504) o el agente falla (502) no se guarda nada y la respuesta trae
    /// "reintentable": true; basta con volver a enviar la misma petición.
    /// </remarks>
    /// <param name="id">Id de la solicitud</param>
    /// <param name="request">Datos del estudiante y documentos (malla, historial y sílabos)</param>
    /// <response code="200">Decisión del agente guardada, por asignatura y con su justificación</response>
    /// <response code="400">Datos de entrada inválidos</response>
    /// <response code="502">El agente externo no se pudo contactar o respondió mal</response>
    /// <response code="504">Venció el tiempo de espera del agente externo</response>
    [HttpPost("{id:int}/analizar-externo")]
    [ProducesResponseType(typeof(ResultadoAnalisisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<ResultadoAnalisisDto>> AnalizarExterno(
        int id, [FromBody] AgenteExternoRequest request, CancellationToken ct)
    {
        if (id <= 0)
            return BadRequest(new { error = "El id de la solicitud debe ser mayor que cero." });

        try
        {
            var resultado = await _servicio.AnalizarSolicitudAsync(id, request, ct);
            return Ok(resultado);
        }
        catch (AgenteExternoTimeoutException ex)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new { error = ex.Message, reintentable = true });
        }
        catch (AgenteExternoException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message, reintentable = true });
        }
    }
}