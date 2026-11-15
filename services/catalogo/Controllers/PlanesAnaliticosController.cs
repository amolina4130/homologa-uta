using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HomologaUTA.Catalogo.Services;

namespace HomologaUTA.Catalogo.Controllers;

[ApiController]
[Route("planes-analiticos")]
public class PlanesAnaliticosController : ControllerBase
{
    private readonly CargaPlanesService _cargaService;

    public PlanesAnaliticosController(CargaPlanesService cargaService)
    {
        _cargaService = cargaService;
    }

    [HttpPost("carga")]
    [Authorize(Roles = "ADMINISTRADOR")]
    public async Task<IActionResult> CargarPlanes([FromForm] List<IFormFile> archivos)
    {
        if (archivos == null || archivos.Count == 0)
            return BadRequest(new { mensaje = "No se enviaron archivos." });

        var resultado = await _cargaService.ProcesarPdfsAsync(archivos);
        return Ok(new { cargados = resultado.cargados, omitidos = resultado.omitidos });
    }
}