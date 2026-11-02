using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Services;
using Microsoft.AspNetCore.Mvc;

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
    /// Por cada asignatura el agente devuelve los porcentajes de contenido, profundidad y carga horaria.
    /// La decisión (HOMOLOGABLE o NO_HOMOLOGABLE) la calcula el código con el umbral del 80 %.
    /// Si el agente no responde bien tras los reintentos, la asignatura queda en REQUIERE_REVISION.
    /// Al terminar se guarda todo, la solicitud pasa a PENDIENTE_REVISION y se publica el evento AnalisisCompletado.
    /// </remarks>
    /// <param name="id">Id de la solicitud</param>
    /// <param name="request">Asignaturas de origen y destino con su contenido</param>
    /// <response code="200">Resultado del análisis por asignatura</response>
    /// <response code="400">Datos de entrada inválidos</response>
    [HttpPost("{id:int}/analizar")]
    [ProducesResponseType(typeof(ResultadoAnalisisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResultadoAnalisisDto>> Analizar(int id, [FromBody] AnalizarSolicitudRequest request)
    {
        if (id <= 0)
            return BadRequest(new { error = "El id de la solicitud debe ser mayor que cero." });

        var resultado = await _servicio.AnalizarSolicitudAsync(id, request.Asignaturas);
        return Ok(resultado);
    }
}