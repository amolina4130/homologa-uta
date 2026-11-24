using HomologaUTA.Solicitudes.Data;
using HomologaUTA.Solicitudes.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Solicitudes.Controllers;

[ApiController]
[Route("memoria")]
public class MemoriaController : ControllerBase
{
    private readonly SolicitudesDbContext _db;
    private readonly MemoriaService _memoria;

    public MemoriaController(SolicitudesDbContext db, MemoriaService memoria)
    {
        _db = db;
        _memoria = memoria;
    }

    /// <summary>Recalcula la memoria del agente; si la huella no cambió no reescribe nada (HU-19).</summary>
    [HttpPost("carga")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Cargar(CancellationToken ct)
    {
        if (!AutorizacionServicio.EsAdministrador(User))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { mensaje = "Solo el Administrador puede cargar la memoria del agente." });

        var resultado = await _memoria.CargarAsync(ct);
        return Ok(new
        {
            cargadas = resultado.Cargadas,
            sinCambios = resultado.SinCambios,
            huella = resultado.Huella
        });
    }

    /// <summary>Estado de la última carga de memoria.</summary>
    [HttpGet("estado")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Estado()
    {
        var estado = await _db.MemoriaEstados.AsNoTracking().FirstOrDefaultAsync();
        if (estado is null)
            return NotFound(new { mensaje = "Todavía no se ha cargado la memoria del agente." });

        return Ok(new
        {
            huella = estado.Huella,
            fechaCarga = estado.FechaCarga,
            totalCarreras = estado.TotalCarreras,
            totalAsignaturas = estado.TotalAsignaturas
        });
    }

    /// <summary>Asignaturas de una carrera en la memoria, filtradas por nivel máximo.</summary>
    [HttpGet("carreras/{carreraId:int}/asignaturas")]
    public async Task<IActionResult> AsignaturasPorCarrera(int carreraId, [FromQuery] int? nivelMaximo)
    {
        var consulta = _db.MemoriaAsignaturas.AsNoTracking().Where(m => m.CarreraId == carreraId);
        if (nivelMaximo is > 0)
            consulta = consulta.Where(m => m.NivelNumerico <= nivelMaximo);

        var lista = await consulta
            .OrderBy(m => m.NivelNumerico)
            .ThenBy(m => m.Nombre)
            .Select(m => new
            {
                asignaturaId = m.AsignaturaId,
                nombre = m.Nombre,
                nivel = m.Nivel,
                area = m.Area,
                creditos = m.Creditos,
                horas = m.Horas,
                contenidosMinimos = m.ContenidosMinimos
            })
            .ToListAsync();

        return Ok(lista);
    }
}
