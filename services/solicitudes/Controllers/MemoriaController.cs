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

    public class PlanAnaliticoRequest
    {
        public string? Contenido { get; set; }
        public string? ResultadosAprendizaje { get; set; }
    }

    /// <summary>
    /// El Administrador carga (o reemplaza) el plan analítico de una asignatura. Se guarda en el
    /// plan y en la memoria del agente a la vez, así la IA lo usa desde el siguiente análisis.
    /// </summary>
    [HttpPut("asignaturas/{asignaturaId:int}/plan")]
    public async Task<IActionResult> GuardarPlan(int asignaturaId, [FromBody] PlanAnaliticoRequest request, CancellationToken ct)
    {
        if (!AutorizacionServicio.EsAdministrador(User))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { mensaje = "Solo el Administrador puede cargar planes analíticos." });

        var contenido = (request?.Contenido ?? string.Empty).Trim();
        var resultados = (request?.ResultadosAprendizaje ?? string.Empty).Trim();
        if (contenido.Length < 40)
            return BadRequest(new { mensaje = "Los contenidos del plan están vacíos o son demasiado cortos." });
        if (contenido.Length > 20000 || resultados.Length > 10000)
            return BadRequest(new { mensaje = "El plan es demasiado extenso." });

        var asignatura = await _db.Asignaturas.FirstOrDefaultAsync(a => a.Id == asignaturaId, ct);
        if (asignatura is null)
            return NotFound(new { mensaje = "La asignatura no existe." });

        var fecha = DateTime.UtcNow;
        var plan = await _db.PlanesAnaliticos.FirstOrDefaultAsync(p => p.AsignaturaId == asignaturaId, ct);
        if (plan is null)
        {
            plan = new Models.PlanAnalitico { AsignaturaId = asignaturaId };
            _db.PlanesAnaliticos.Add(plan);
        }
        plan.Contenido = contenido;
        plan.ResultadosAprendizaje = resultados;
        plan.FechaCarga = fecha;

        foreach (var fila in await _db.MemoriaAsignaturas.Where(m => m.AsignaturaId == asignaturaId).ToListAsync(ct))
        {
            fila.ContenidosMinimos = contenido;
            fila.FechaActualizacion = fecha;
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { asignaturaId, nombre = asignatura.Nombre, fechaCarga = fecha });
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

        // Los resultados de aprendizaje del plan analítico se usan para medir la profundidad (Art. 20).
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
                contenidosMinimos = m.ContenidosMinimos,
                resultadosAprendizaje = _db.PlanesAnaliticos
                    .Where(p => p.AsignaturaId == m.AsignaturaId)
                    .Select(p => p.ResultadosAprendizaje)
                    .FirstOrDefault() ?? string.Empty
            })
            .ToListAsync();

        return Ok(lista);
    }
}
