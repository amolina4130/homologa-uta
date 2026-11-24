using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomologaUTA.Homologacion.Controllers;

/// <summary>
/// Ayuda del agente interno durante la selección de carreras, antes de que exista una
/// solicitud. Por eso no depende de delegaciones ni de un id de solicitud.
/// </summary>
[ApiController]
[Route("homologacion/recomendacion-carrera")]
public class RecomendacionCarreraController : ControllerBase
{
    private readonly DocenteService _servicio;

    public RecomendacionCarreraController(DocenteService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>
    /// Recomienda qué materias se pueden homologar usando las mallas, planes analíticos,
    /// créditos y horas reales ya cargados en la memoria del agente interno.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<RecomendacionIAInternaDto>> Recomendar(
        [FromBody] RecomendacionCarreraRequest request, CancellationToken ct)
    {
        if (!AutorizacionHomologacion.EsGestor(User))
            return Forbid();

        try
        {
            return Ok(await _servicio.ObtenerRecomendacionPorCarreraAsync(request, ct));
        }
        catch (InformeException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }
}
