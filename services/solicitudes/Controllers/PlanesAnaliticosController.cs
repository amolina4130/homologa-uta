using HomologaUTA.Solicitudes.DTOs;
using HomologaUTA.Solicitudes.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomologaUTA.Solicitudes.Controllers;

[ApiController]
[Route("planes-analiticos")]
public class PlanesAnaliticosController : ControllerBase
{
    private readonly CargaPlanesService _carga;
    private readonly ILogger<PlanesAnaliticosController> _logger;

    public PlanesAnaliticosController(CargaPlanesService carga, ILogger<PlanesAnaliticosController> logger)
    {
        _carga = carga;
        _logger = logger;
    }

    /// <summary>Carga los planes analíticos desde uno o varios PDF (HU-13, solo Administrador)</summary>
    /// <remarks>
    /// El archivo se asocia a la asignatura por el prefijo numérico del nombre (por ejemplo
    /// <c>12_plan.pdf</c>) o por el nombre de la asignatura que aparece en el texto. Los PDF que no
    /// se pueden asociar se devuelven en <c>omitidos</c> y no detienen la carga.
    /// </remarks>
    /// <response code="200">Resultado de la carga</response>
    /// <response code="400">No se enviaron archivos</response>
    /// <response code="403">El usuario no es Administrador</response>
    [HttpPost("carga")]
    [RequestSizeLimit(100_000_000)]
    [ProducesResponseType(typeof(CargaPlanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Cargar([FromForm] List<IFormFile> archivos, CancellationToken ct)
    {
        if (EsRolDistintoDeAdministrador())
            return StatusCode(StatusCodes.Status403Forbidden, new { mensaje = "Solo el Administrador puede cargar planes analíticos." });

        var lista = archivos?.Where(a => a is { Length: > 0 }).ToList() ?? new List<IFormFile>();
        if (lista.Count == 0)
            return BadRequest(new { mensaje = "Envíe al menos un archivo PDF en el campo 'archivos'." });

        var respuesta = await _carga.CargarAsync(lista, ct);
        _logger.LogInformation("Carga de planes: {Cargados} cargados, {Omitidos} omitidos", respuesta.Cargados, respuesta.Omitidos.Count);
        return Ok(respuesta);
    }

    private bool EsRolDistintoDeAdministrador() =>
        User.FindFirst("rol") is { } rol && rol.Value != "ADMINISTRADOR";
}
