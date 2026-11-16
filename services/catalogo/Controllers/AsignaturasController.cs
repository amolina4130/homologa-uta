using Microsoft.AspNetCore.Mvc;
using HomologaUTA.Catalogo.Data;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Catalogo.Controllers;

[ApiController]
[Route("asignaturas")]
public class AsignaturasController : ControllerBase
{
    private readonly CatalogoDbContext _db;

    public AsignaturasController(CatalogoDbContext db)
    {
        _db = db;
    }

    [HttpGet("{id}/plan-analitico")]
    public async Task<IActionResult> ObtenerPlanAnalitico(int id)
    {
        var plan = await _db.PlanesAnaliticos
            .Where(p => p.AsignaturaId == id && p.EsVigente)
            .Select(p => new { p.AsignaturaId, p.Contenido, p.ResultadosAprendizaje, p.FechaCarga })
            .FirstOrDefaultAsync();

        if (plan == null)
            return NotFound(new { mensaje = "No hay plan analítico para esta asignatura." });

        return Ok(plan);
    }
}