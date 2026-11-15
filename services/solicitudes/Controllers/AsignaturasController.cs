using HomologaUTA.Solicitudes.Data;
using HomologaUTA.Solicitudes.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Solicitudes.Controllers;

[ApiController]
[Route("asignaturas")]
public class AsignaturasController : ControllerBase
{
    private readonly SolicitudesDbContext _db;

    public AsignaturasController(SolicitudesDbContext db)
    {
        _db = db;
    }

    /// <summary>Plan analítico vigente de una asignatura (HU-13)</summary>
    /// <response code="200">Plan analítico encontrado</response>
    /// <response code="404">La asignatura no existe o todavía no tiene plan</response>
    [HttpGet("{id:int}/plan-analitico")]
    [ProducesResponseType(typeof(PlanAnaliticoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerPlanAnalitico(int id)
    {
        var asignaturaExiste = await _db.Asignaturas.AnyAsync(a => a.Id == id);
        if (!asignaturaExiste)
            return NotFound(new { mensaje = "Asignatura no encontrada." });

        var plan = await _db.PlanesAnaliticos
            .AsNoTracking()
            .Where(p => p.AsignaturaId == id)
            .Select(p => new PlanAnaliticoDto
            {
                AsignaturaId = p.AsignaturaId,
                Contenido = p.Contenido,
                ResultadosAprendizaje = p.ResultadosAprendizaje,
                FechaCarga = p.FechaCarga
            })
            .FirstOrDefaultAsync();

        if (plan is null)
            return NotFound(new { mensaje = "La asignatura no tiene plan analítico cargado." });

        return Ok(plan);
    }
}
