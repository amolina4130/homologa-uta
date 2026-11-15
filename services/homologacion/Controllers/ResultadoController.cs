using System.Security.Claims;
using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomologaUTA.Homologacion.Controllers;

/// <summary>Resultado del análisis y análisis guiado por la memoria del agente (HU-14/HU-19/HU-21/HU-22).</summary>
[ApiController]
[Route("homologacion")]
public class ResultadoController : ControllerBase
{
    private readonly AnalisisMemoriaService _servicio;

    public ResultadoController(AnalisisMemoriaService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>Resultado del análisis guardado para una solicitud</summary>
    /// <response code="200">Resultado por asignatura</response>
    /// <response code="404">La solicitud todavía no tiene análisis</response>
    [HttpGet("{id:int}/resultado")]
    [ProducesResponseType(typeof(ResultadoDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerResultado(int id, CancellationToken ct)
    {
        var resultado = await _servicio.ObtenerResultadoAsync(id, ct);
        return resultado is null
            ? NotFound(new { mensaje = "La solicitud todavía no tiene un análisis." })
            : Ok(resultado);
    }

    /// <summary>Analiza las asignaturas contra la memoria del agente, las equivalencias y el plan analítico</summary>
    /// <remarks>
    /// Aplica primero las equivalencias activas, preselecciona candidatas por área y nivel para no
    /// enviar toda la carga al agente y permite combinar una o dos materias de origen por materia de destino.
    /// Al terminar deja la solicitud en PENDIENTE_REVISION.
    /// </remarks>
    /// <response code="200">Resultado guardado</response>
    /// <response code="400">Combinación inválida (más de dos materias de origen)</response>
    /// <response code="404">La solicitud no existe</response>
    /// <response code="409">La memoria del agente no está cargada</response>
    [HttpPost("{id:int}/analizar-memoria")]
    [ProducesResponseType(typeof(ResultadoDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Analizar(int id, [FromBody] AnalisisMemoriaRequest? request, CancellationToken ct)
    {
        try
        {
            var resultado = await _servicio.AnalizarAsync(
                id, request ?? new AnalisisMemoriaRequest(), Correo() ?? "sistema", ct);
            return Ok(resultado);
        }
        catch (AnalisisMemoriaException ex)
        {
            return StatusCode(ex.StatusCode, new { mensaje = ex.Message });
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { mensaje = ex.Message });
        }
        catch (CatalogoNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { mensaje = ex.Message });
        }
    }

    private string? Correo() =>
        User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
}
