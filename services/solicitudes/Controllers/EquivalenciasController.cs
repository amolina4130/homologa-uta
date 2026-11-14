using HomologaUTA.Solicitudes.Data;
using HomologaUTA.Solicitudes.Models;
using HomologaUTA.Solicitudes.Services;
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

    /// <summary>Lista las equivalencias entre carreras (HU-14).</summary>
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] bool? activa)
    {
        var consulta = _db.Equivalencias
            .AsNoTracking()
            .Include(e => e.CarreraOrigen)
            .Include(e => e.CarreraDestino)
            .Include(e => e.AsignaturaOrigen)
            .Include(e => e.AsignaturaDestino)
            .AsQueryable();

        if (activa.HasValue)
            consulta = consulta.Where(e => e.Activa == activa.Value);

        var lista = await consulta
            .OrderBy(e => e.Id)
            .Select(e => new
            {
                e.Id,
                e.CarreraOrigenId,
                carreraOrigen = e.CarreraOrigen.Nombre,
                e.AsignaturaOrigenId,
                asignaturaOrigen = e.AsignaturaOrigen.Nombre,
                e.CarreraDestinoId,
                carreraDestino = e.CarreraDestino.Nombre,
                e.AsignaturaDestinoId,
                asignaturaDestino = e.AsignaturaDestino.Nombre,
                e.Activa,
                e.FechaCreacion
            })
            .ToListAsync();

        return Ok(lista);
    }

    /// <summary>Busca la equivalencia activa entre dos asignaturas (la usa Homologación).</summary>
    [HttpGet("buscar")]
    public async Task<IActionResult> Buscar([FromQuery] int origenId, [FromQuery] int destinoId)
    {
        var equivalencia = await _db.Equivalencias
            .AsNoTracking()
            .Include(e => e.AsignaturaOrigen)
            .Include(e => e.AsignaturaDestino)
            .FirstOrDefaultAsync(e => e.AsignaturaOrigenId == origenId
                                      && e.AsignaturaDestinoId == destinoId
                                      && e.Activa);

        return equivalencia is null
            ? NotFound(new { mensaje = "No existe una equivalencia activa entre las asignaturas." })
            : Ok(new
            {
                equivalencia.Id,
                equivalencia.CarreraOrigenId,
                equivalencia.AsignaturaOrigenId,
                equivalencia.CarreraDestinoId,
                equivalencia.AsignaturaDestinoId,
                equivalencia.Activa,
                regla = $"Equivalencia registrada: {equivalencia.AsignaturaOrigen.Nombre} → {equivalencia.AsignaturaDestino.Nombre}"
            });
    }

    /// <summary>Registra una equivalencia (solo Administrador).</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear([FromBody] EquivalenciaRequest request)
    {
        if (!AutorizacionServicio.EsAdministrador(User))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { mensaje = "Solo el Administrador puede registrar equivalencias." });

        var error = await ValidarAsync(request);
        if (error is not null)
            return BadRequest(new { mensaje = error });

        var duplicada = await _db.Equivalencias.AnyAsync(e =>
            e.CarreraOrigenId == request.CarreraOrigenId &&
            e.AsignaturaOrigenId == request.AsignaturaOrigenId &&
            e.CarreraDestinoId == request.CarreraDestinoId &&
            e.AsignaturaDestinoId == request.AsignaturaDestinoId);

        if (duplicada)
            return Conflict(new { mensaje = "Ya existe una equivalencia entre esas mismas asignaturas." });

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

        return Created($"/equivalencias/{equivalencia.Id}", new { id = equivalencia.Id });
    }

    /// <summary>Activa, desactiva o cambia una equivalencia (solo Administrador).</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] EquivalenciaRequest request)
    {
        if (!AutorizacionServicio.EsAdministrador(User))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { mensaje = "Solo el Administrador puede modificar equivalencias." });

        var equivalencia = await _db.Equivalencias.FirstOrDefaultAsync(e => e.Id == id);
        if (equivalencia is null)
            return NotFound(new { mensaje = "Equivalencia no encontrada." });

        var error = await ValidarAsync(request);
        if (error is not null)
            return BadRequest(new { mensaje = error });

        var duplicada = await _db.Equivalencias.AnyAsync(e =>
            e.Id != id &&
            e.CarreraOrigenId == request.CarreraOrigenId &&
            e.AsignaturaOrigenId == request.AsignaturaOrigenId &&
            e.CarreraDestinoId == request.CarreraDestinoId &&
            e.AsignaturaDestinoId == request.AsignaturaDestinoId);

        if (duplicada)
            return Conflict(new { mensaje = "Ya existe una equivalencia entre esas mismas asignaturas." });

        equivalencia.CarreraOrigenId = request.CarreraOrigenId;
        equivalencia.AsignaturaOrigenId = request.AsignaturaOrigenId;
        equivalencia.CarreraDestinoId = request.CarreraDestinoId;
        equivalencia.AsignaturaDestinoId = request.AsignaturaDestinoId;
        equivalencia.Activa = request.Activa;

        await _db.SaveChangesAsync();
        return Ok(new { id = equivalencia.Id, equivalencia.Activa });
    }

    private async Task<string?> ValidarAsync(EquivalenciaRequest request)
    {
        if (request is null)
            return "Los datos de la equivalencia son obligatorios.";

        var carreraOrigen = await _db.Carreras.AnyAsync(c => c.Id == request.CarreraOrigenId);
        var carreraDestino = await _db.Carreras.AnyAsync(c => c.Id == request.CarreraDestinoId);
        if (!carreraOrigen || !carreraDestino)
            return "La carrera de origen o de destino no existe.";

        var origen = await _db.Asignaturas.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.AsignaturaOrigenId);
        var destino = await _db.Asignaturas.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.AsignaturaDestinoId);

        if (origen is null || destino is null)
            return "La asignatura de origen o de destino no existe.";
        if (origen.CarreraId != request.CarreraOrigenId)
            return "La asignatura de origen no pertenece a la carrera de origen.";
        if (destino.CarreraId != request.CarreraDestinoId)
            return "La asignatura de destino no pertenece a la carrera de destino.";
        if (request.AsignaturaOrigenId == request.AsignaturaDestinoId)
            return "La asignatura de origen y la de destino no pueden ser la misma.";

        return null;
    }
}

public class EquivalenciaRequest
{
    public int CarreraOrigenId { get; set; }
    public int AsignaturaOrigenId { get; set; }
    public int CarreraDestinoId { get; set; }
    public int AsignaturaDestinoId { get; set; }
    public bool Activa { get; set; } = true;
}
