using HomologaUTA.Solicitudes.Data;
using HomologaUTA.Solicitudes.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Solicitudes.Controllers;

[ApiController]
public class PlanesAnaliticosController : ControllerBase
{
    private readonly SolicitudesDbContext _db;
    private readonly CargaPlanesService _carga;

    public PlanesAnaliticosController(SolicitudesDbContext db, CargaPlanesService carga)
    {
        _db = db;
        _carga = carga;
    }

    /// <summary>Plan analítico vigente de una asignatura (HU-13).</summary>
    [HttpGet("asignaturas/{id:int}/plan-analitico")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerPlan(int id)
    {
        var plan = await _db.PlanesAnaliticos
            .AsNoTracking()
            .Where(p => p.AsignaturaId == id)
            .Select(p => new
            {
                asignaturaId = p.AsignaturaId,
                contenido = p.Contenido,
                resultadosAprendizaje = p.ResultadosAprendizaje,
                fechaCarga = p.FechaCarga
            })
            .FirstOrDefaultAsync();

        return plan is null
            ? NotFound(new { mensaje = "La asignatura no tiene un plan analítico cargado." })
            : Ok(plan);
    }

    /// <summary>Carga masiva de planes analíticos desde PDF (solo Administrador).</summary>
    [HttpPost("planes-analiticos/carga")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Cargar([FromForm] List<IFormFile> archivos, CancellationToken ct)
    {
        if (!AutorizacionServicio.EsAdministrador(User))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { mensaje = "Solo el Administrador puede cargar planes analíticos." });

        if (archivos is null || archivos.Count == 0)
            return BadRequest(new { mensaje = "Adjunte al menos un archivo PDF." });

        var resultado = await _carga.CargarAsync(archivos, ct);
        return Ok(new
        {
            cargados = resultado.Cargados,
            omitidos = resultado.Omitidos.Select(o => new { archivo = o.Archivo, motivo = o.Motivo })
        });
    }
}
