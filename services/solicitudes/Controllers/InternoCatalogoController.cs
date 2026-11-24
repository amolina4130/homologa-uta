using HomologaUTA.Solicitudes.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Solicitudes.Controllers;

/// <summary>
/// Catálogo y memoria del agente interno para consultas entre servicios. No se publica en el
/// Gateway: solo lo alcanzan los servicios de la red Docker (y localhost en desarrollo).
/// </summary>
[ApiController]
[Route("internal")]
public class InternoCatalogoController : ControllerBase
{
    private readonly SolicitudesDbContext _db;

    public InternoCatalogoController(SolicitudesDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Asignaturas reales de una carrera con créditos, horas, área y contenido del plan
    /// analítico. Es la información que el agente interno recibe como texto en memoria.
    /// </summary>
    [HttpGet("carreras/{carreraId:int}/asignaturas")]
    public async Task<IActionResult> Asignaturas(int carreraId, CancellationToken ct)
    {
        if (carreraId <= 0)
            return BadRequest(new { error = "El id de la carrera debe ser mayor que cero." });

        var existe = await _db.Carreras.AnyAsync(c => c.Id == carreraId, ct);
        if (!existe)
            return NotFound(new { error = "Carrera no encontrada." });

        var planes = await _db.PlanesAnaliticos
            .AsNoTracking()
            .ToDictionaryAsync(p => p.AsignaturaId, ct);

        var memoria = await _db.MemoriaAsignaturas
            .AsNoTracking()
            .Where(m => m.CarreraId == carreraId)
            .ToDictionaryAsync(m => m.AsignaturaId, ct);

        var asignaturas = await _db.Asignaturas
            .AsNoTracking()
            .Where(a => a.CarreraId == carreraId)
            .OrderBy(a => a.Nivel)
            .ThenBy(a => a.Nombre)
            .ToListAsync(ct);

        var resultado = asignaturas.Select(a =>
        {
            planes.TryGetValue(a.Id, out var plan);
            memoria.TryGetValue(a.Id, out var filaMemoria);

            return new
            {
                asignaturaId = a.Id,
                nombre = a.Nombre,
                nivel = a.Nivel,
                area = string.IsNullOrWhiteSpace(a.Area) ? filaMemoria?.Area ?? string.Empty : a.Area,
                creditos = a.Creditos,
                horas = a.Horas,
                contenido = string.IsNullOrWhiteSpace(plan?.Contenido)
                    ? filaMemoria?.ContenidosMinimos ?? string.Empty
                    : plan!.Contenido
            };
        }).ToList();

        return Ok(resultado);
    }

    /// <summary>Huella de la última carga de la memoria del agente interno.</summary>
    [HttpGet("memoria/estado")]
    public async Task<IActionResult> EstadoMemoria(CancellationToken ct)
    {
        var estado = await _db.MemoriaEstados.AsNoTracking().FirstOrDefaultAsync(ct);
        if (estado is null)
            return Ok(new { huella = string.Empty, totalAsignaturas = 0 });

        return Ok(new { huella = estado.Huella, totalAsignaturas = estado.TotalAsignaturas });
    }
}
