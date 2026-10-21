using HomologaUTA.Solicitudes.Data;
using HomologaUTA.Solicitudes.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Solicitudes.Controllers;

[ApiController]
[Route("carreras")]
// [Authorize] <--- LO QUITAMOS PARA QUE NO EXPLOTE (El Gateway ya protege)
public class CarrerasController : ControllerBase
{
    private readonly SolicitudesDbContext _db;

    public CarrerasController(SolicitudesDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerCarreras()
    {
        var carreras = await _db.Carreras
            .Select(c => new CarreraDto
            {
                Id = c.Id,
                Nombre = c.Nombre
            })
            .ToListAsync();

        return Ok(carreras);
    }

    [HttpGet("{id}/asignaturas")]
    public async Task<IActionResult> ObtenerAsignaturas(int id)
    {
        var carreraExiste = await _db.Carreras.AnyAsync(c => c.Id == id);
        if (!carreraExiste)
            return NotFound(new { error = "Carrera no encontrada." });

        var asignaturas = await _db.Asignaturas
            .Where(a => a.CarreraId == id)
            .Select(a => new AsignaturaDto
            {
                Id = a.Id,
                Nombre = a.Nombre,
                Nivel = a.Nivel,
                Creditos = a.Creditos,
                Horas = a.Horas
            })
            .ToListAsync();

        return Ok(asignaturas);
    }
}