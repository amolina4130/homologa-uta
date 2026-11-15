using HomologaUTA.Solicitudes.Data;
using HomologaUTA.Solicitudes.DTOs;
using HomologaUTA.Solicitudes.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Solicitudes.Controllers;

[ApiController]
[Route("equivalencias")]
public class EquivalenciasController : ControllerBase
{
    private readonly SolicitudesDbContext _db;

    public EquivalenciasController(SolicitudesDbContext db)
    {
        _db = db;
    }

    /// <summary>Lista las equivalencias registradas (HU-14)</summary>
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int? carreraOrigenId,
        [FromQuery] int? carreraDestinoId,
        [FromQuery] bool? activa)
    {
        var query = _db.Equivalencias.AsNoTracking().AsQueryable();
        if (carreraOrigenId.HasValue) query = query.Where(e => e.CarreraOrigenId == carreraOrigenId.Value);
        if (carreraDestinoId.HasValue) query = query.Where(e => e.CarreraDestinoId == carreraDestinoId.Value);
        if (activa.HasValue) query = query.Where(e => e.Activa == activa.Value);

        var lista = await Proyectar(query.OrderBy(e => e.Id)).ToListAsync();
        return Ok(lista);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var equivalencia = await Proyectar(_db.Equivalencias.AsNoTracking().Where(e => e.Id == id))
            .FirstOrDefaultAsync();
        return equivalencia is null
            ? NotFound(new { mensaje = "Equivalencia no encontrada." })
            : Ok(equivalencia);
    }

    /// <summary>Registra una equivalencia entre asignaturas de dos carreras (solo Administrador)</summary>
    /// <response code="201">Equivalencia creada</response>
    /// <response code="400">Datos inválidos</response>
    /// <response code="403">El usuario no es Administrador</response>
    /// <response code="409">Ya existe una equivalencia entre las mismas asignaturas</response>
    [HttpPost]
    [ProducesResponseType(typeof(EquivalenciaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear([FromBody] EquivalenciaRequest request)
    {
        if (EsRolDistintoDeAdministrador())
            return StatusCode(StatusCodes.Status403Forbidden, new { mensaje = "Solo el Administrador puede crear equivalencias." });

        var error = await ValidarAsync(request);
        if (error is not null)
            return BadRequest(new { mensaje = error });

        var duplicada = await _db.Equivalencias.AnyAsync(e =>
            e.AsignaturaOrigenId == request.AsignaturaOrigenId &&
            e.AsignaturaDestinoId == request.AsignaturaDestinoId);
        if (duplicada)
            return Conflict(new { mensaje = "Ya existe una equivalencia entre estas dos asignaturas." });

        var equivalencia = new Equivalencia
        {
            CarreraOrigenId = request.CarreraOrigenId,
            AsignaturaOrigenId = request.AsignaturaOrigenId,
            CarreraDestinoId = request.CarreraDestinoId,
            AsignaturaDestinoId = request.AsignaturaDestinoId,
            Activa = request.Activa,
            FechaCreacion = DateTime.UtcNow
        };
        _db.Equivalencias.Add(equivalencia);
        await _db.SaveChangesAsync();

        var dto = await Proyectar(_db.Equivalencias.AsNoTracking().Where(e => e.Id == equivalencia.Id))
            .FirstAsync();
        return CreatedAtAction(nameof(Obtener), new { id = equivalencia.Id }, dto);
    }

    /// <summary>Modifica una equivalencia, incluido su estado activa/inactiva (solo Administrador)</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(EquivalenciaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Actualizar(int id, [FromBody] EquivalenciaRequest request)
    {
        if (EsRolDistintoDeAdministrador())
            return StatusCode(StatusCodes.Status403Forbidden, new { mensaje = "Solo el Administrador puede modificar equivalencias." });

        var equivalencia = await _db.Equivalencias.FirstOrDefaultAsync(e => e.Id == id);
        if (equivalencia is null)
            return NotFound(new { mensaje = "Equivalencia no encontrada." });

        var error = await ValidarAsync(request);
        if (error is not null)
            return BadRequest(new { mensaje = error });

        var duplicada = await _db.Equivalencias.AnyAsync(e =>
            e.Id != id &&
            e.AsignaturaOrigenId == request.AsignaturaOrigenId &&
            e.AsignaturaDestinoId == request.AsignaturaDestinoId);
        if (duplicada)
            return Conflict(new { mensaje = "Ya existe una equivalencia entre estas dos asignaturas." });

        equivalencia.CarreraOrigenId = request.CarreraOrigenId;
        equivalencia.AsignaturaOrigenId = request.AsignaturaOrigenId;
        equivalencia.CarreraDestinoId = request.CarreraDestinoId;
        equivalencia.AsignaturaDestinoId = request.AsignaturaDestinoId;
        equivalencia.Activa = request.Activa;

        await _db.SaveChangesAsync();

        var dto = await Proyectar(_db.Equivalencias.AsNoTracking().Where(e => e.Id == id)).FirstAsync();
        return Ok(dto);
    }

    private async Task<string?> ValidarAsync(EquivalenciaRequest request)
    {
        if (request.CarreraOrigenId <= 0 || request.CarreraDestinoId <= 0 ||
            request.AsignaturaOrigenId <= 0 || request.AsignaturaDestinoId <= 0)
            return "Todos los identificadores son obligatorios.";

        if (request.CarreraOrigenId == request.CarreraDestinoId)
            return "La carrera de origen y destino no pueden ser iguales.";

        var carreras = await _db.Carreras.CountAsync(c =>
            c.Id == request.CarreraOrigenId || c.Id == request.CarreraDestinoId);
        if (carreras < 2)
            return "La carrera de origen o destino no existe.";

        var origen = await _db.Asignaturas.AsNoTracking().FirstOrDefaultAsync(a => a.Id == request.AsignaturaOrigenId);
        var destino = await _db.Asignaturas.AsNoTracking().FirstOrDefaultAsync(a => a.Id == request.AsignaturaDestinoId);
        if (origen is null || destino is null)
            return "La asignatura de origen o destino no existe.";
        if (origen.CarreraId != request.CarreraOrigenId)
            return "La asignatura de origen no pertenece a la carrera de origen.";
        if (destino.CarreraId != request.CarreraDestinoId)
            return "La asignatura de destino no pertenece a la carrera de destino.";

        return null;
    }

    private static IQueryable<EquivalenciaDto> Proyectar(IQueryable<Equivalencia> query) =>
        query.Select(e => new EquivalenciaDto
        {
            Id = e.Id,
            CarreraOrigenId = e.CarreraOrigenId,
            CarreraOrigen = e.CarreraOrigen.Nombre,
            AsignaturaOrigenId = e.AsignaturaOrigenId,
            AsignaturaOrigen = e.AsignaturaOrigen.Nombre,
            CarreraDestinoId = e.CarreraDestinoId,
            CarreraDestino = e.CarreraDestino.Nombre,
            AsignaturaDestinoId = e.AsignaturaDestinoId,
            AsignaturaDestino = e.AsignaturaDestino.Nombre,
            Activa = e.Activa
        });

    private bool EsRolDistintoDeAdministrador() =>
        User.FindFirst("rol") is { } rol && rol.Value != "ADMINISTRADOR";
}
