using HomologaUTA.Solicitudes.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Solicitudes.Controllers;

/// <summary>
/// Consultas entre servicios. El Gateway no tiene ninguna ruta /internal, así que solo se alcanza
/// desde la red interna de Docker.
/// </summary>
[ApiController]
[Route("internal/solicitudes")]
public class InternoController : ControllerBase
{
    private const int MaxIds = 200;

    private readonly SolicitudesDbContext _db;

    public InternoController(SolicitudesDbContext db)
    {
        _db = db;
    }

    /// <summary>Datos de varias solicitudes a la vez (fecha, estudiante, tipo y carreras)</summary>
    /// <param name="ids">Ids separados por coma, por ejemplo 1,2,3</param>
    [HttpGet]
    public async Task<IActionResult> ObtenerPorIds([FromQuery] string? ids)
    {
        var lista = (ids ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var n) ? n : 0)
            .Where(n => n > 0)
            .Distinct()
            .ToList();

        if (lista.Count == 0)
            return BadRequest(new { error = "Envíe los ids de las solicitudes separados por coma." });

        if (lista.Count > MaxIds)
            return BadRequest(new { error = $"Máximo {MaxIds} solicitudes por consulta." });

        var solicitudes = await _db.Solicitudes
            .Where(s => lista.Contains(s.Id))
            .Select(s => new SolicitudInternaDto
            {
                Id = s.Id,
                Tipo = s.Tipo,
                EstudianteCorreo = s.EstudianteCorreo,
                FechaRegistro = s.FechaRegistro,
                CarreraOrigen = s.CarreraOrigen.Nombre,
                CarreraDestino = s.CarreraDestino.Nombre,
                Estado = s.Estado
            })
            .ToListAsync();

        return Ok(solicitudes);
    }
}

public class SolicitudInternaDto
{
    public int Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string EstudianteCorreo { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
    public string CarreraOrigen { get; set; } = string.Empty;
    public string CarreraDestino { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
}