using HomologaUTA.Reglas.Data;
using HomologaUTA.Reglas.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Reglas.Controllers;

[ApiController]
[Route("reglas")]
// El Gateway ya valida el token: GET para cualquier usuario autenticado
// y PUT solo para el rol ADMINISTRADOR (política "administrador").
public class ReglasController : ControllerBase
{
    private readonly ReglasDbContext _db;

    public ReglasController(ReglasDbContext db)
    {
        _db = db;
    }

    /// <summary>Devuelve el catálogo de reglas (RN-01 a RN-11) con su fuente y su estado</summary>
    [HttpGet]
    public async Task<ActionResult<List<ReglaDto>>> ObtenerReglas()
    {
        // Se lee de la base en cada llamada: un cambio rige en el siguiente análisis sin recompilar.
        var reglas = await _db.Reglas
            .AsNoTracking()
            .OrderBy(r => r.Codigo)
            .Select(r => new ReglaDto
            {
                Id = r.Id,
                Codigo = r.Codigo,
                Descripcion = r.Descripcion,
                Fuente = r.Fuente,
                Umbral = r.Umbral,
                Activa = r.Activa
            })
            .ToListAsync();

        return Ok(reglas);
    }

    /// <summary>Cambia el umbral y/o el estado activo o inactivo de una regla (solo Administrador)</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ReglaDto>> ActualizarRegla(int id, [FromBody] ReglaActualizarDto dto)
    {
        var regla = await _db.Reglas.FirstOrDefaultAsync(r => r.Id == id);
        if (regla is null)
            return NotFound(new { error = "Regla no encontrada." });

        if (dto.Umbral.HasValue)
        {
            if (regla.Umbral is null)
                return BadRequest(new { error = $"La regla {regla.Codigo} no tiene umbral configurable." });

            if (dto.Umbral.Value < 0)
                return BadRequest(new { error = "El umbral no puede ser negativo." });

            // RN-03 es un porcentaje
            if (regla.Codigo == "RN-03" && dto.Umbral.Value > 100)
                return BadRequest(new { error = "El umbral de RN-03 es un porcentaje y no puede superar 100." });

            regla.Umbral = dto.Umbral.Value;
        }

        regla.Activa = dto.Activa!.Value;
        await _db.SaveChangesAsync();

        return Ok(new ReglaDto
        {
            Id = regla.Id,
            Codigo = regla.Codigo,
            Descripcion = regla.Descripcion,
            Fuente = regla.Fuente,
            Umbral = regla.Umbral,
            Activa = regla.Activa
        });
    }
}
