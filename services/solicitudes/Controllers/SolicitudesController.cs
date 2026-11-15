using HomologaUTA.Solicitudes.Data;
using HomologaUTA.Solicitudes.DTOs;
using HomologaUTA.Solicitudes.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HomologaUTA.Solicitudes.Controllers;

[ApiController]
[Route("solicitudes")]
// [Authorize] <--- Lo dejamos comentado: el Gateway protege la ruta y aquí solo se leen los claims
public class SolicitudesController : ControllerBase
{
    private readonly SolicitudesDbContext _db;

    public SolicitudesController(SolicitudesDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> CrearSolicitud([FromBody] SolicitudRequest request)
    {
        var validacion = await ValidarRequestAsync(request);
        if (validacion is not null)
            return BadRequest(new { error = validacion });

        var correo = CorreoActual() ?? "estudiante@uta.edu.ec";
        var modulo = NormalizarModulo(request.Modulo);

        var solicitud = new Solicitud
        {
            Tipo = "Homologación",
            Modulo = modulo,
            CarreraOrigenId = request.CarreraOrigenId,
            CarreraDestinoId = request.CarreraDestinoId,
            UniversidadOrigen = modulo == "EXTERNO" ? request.UniversidadOrigen?.Trim() : null,
            UniversidadDestino = modulo == "EXTERNO" ? request.UniversidadDestino?.Trim() : null,
            EstudianteCorreo = correo,
            Estado = EstadoSolicitud.REGISTRADA
        };

        foreach (var asigId in request.AsignaturasIds.Distinct())
            solicitud.Asignaturas.Add(new SolicitudAsignatura { AsignaturaId = asigId });

        _db.Solicitudes.Add(solicitud);

        _db.HistorialEstados.Add(new HistorialEstado
        {
            Solicitud = solicitud,
            EstadoAnterior = null,
            EstadoNuevo = solicitud.Estado,
            UsuarioCorreo = correo,
            FechaCambio = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        // El número de proceso se genera con el Id: es único y legible (SOL-0004)
        solicitud.NumeroProceso = $"SOL-{solicitud.Id:D4}";
        await _db.SaveChangesAsync();

        return Created($"/solicitudes/{solicitud.Id}", new
        {
            id = solicitud.Id,
            numeroProceso = solicitud.NumeroProceso,
            mensaje = "Solicitud registrada exitosamente."
        });
    }

    /// <summary>Edita una solicitud mientras siga en estado REGISTRADA (HU-16).</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> EditarSolicitud(int id, [FromBody] SolicitudRequest request)
    {
        var solicitud = await _db.Solicitudes
            .Include(s => s.Asignaturas)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud is null)
            return NotFound(new { error = "Solicitud no encontrada." });

        var correo = CorreoActual();
        if (!string.IsNullOrEmpty(correo) && !string.Equals(correo, solicitud.EstudianteCorreo, StringComparison.OrdinalIgnoreCase)
            && !EsCoordinadorOAdministrador())
            return StatusCode(StatusCodes.Status403Forbidden, new { mensaje = "No puede editar una solicitud de otro estudiante." });

        if (!EstadoSolicitud.EsEditable(solicitud.Estado))
            return Conflict(new { mensaje = "La solicitud ya pasó a revisión y no se puede editar." });

        var validacion = await ValidarRequestAsync(request);
        if (validacion is not null)
            return BadRequest(new { error = validacion });

        var modulo = NormalizarModulo(request.Modulo);
        solicitud.Modulo = modulo;
        solicitud.CarreraOrigenId = request.CarreraOrigenId;
        solicitud.CarreraDestinoId = request.CarreraDestinoId;
        solicitud.UniversidadOrigen = modulo == "EXTERNO" ? request.UniversidadOrigen?.Trim() : null;
        solicitud.UniversidadDestino = modulo == "EXTERNO" ? request.UniversidadDestino?.Trim() : null;

        _db.SolicitudAsignaturas.RemoveRange(solicitud.Asignaturas);
        solicitud.Asignaturas.Clear();
        foreach (var asigId in request.AsignaturasIds.Distinct())
            solicitud.Asignaturas.Add(new SolicitudAsignatura { AsignaturaId = asigId });

        _db.HistorialEstados.Add(new HistorialEstado
        {
            SolicitudId = solicitud.Id,
            EstadoAnterior = solicitud.Estado,
            EstadoNuevo = solicitud.Estado,
            UsuarioCorreo = correo ?? solicitud.EstudianteCorreo,
            FechaCambio = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        var detalle = await ObtenerDetalleInternoAsync(id);
        return Ok(detalle);
    }

    [HttpGet("mias")]
    public async Task<IActionResult> ObtenerMisSolicitudes(
        [FromQuery] string? estado,
        [FromQuery] int? carreraId,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta)
    {
        var correo = CorreoActual() ?? "estudiante@uta.edu.ec";

        var query = _db.Solicitudes
            .AsNoTracking()
            .Include(s => s.CarreraOrigen)
            .Include(s => s.CarreraDestino)
            .Where(s => s.EstudianteCorreo == correo);

        if (!string.IsNullOrWhiteSpace(estado))
            query = query.Where(s => s.Estado == estado);
        if (carreraId.HasValue)
            query = query.Where(s => s.CarreraOrigenId == carreraId || s.CarreraDestinoId == carreraId);
        if (desde.HasValue)
            query = query.Where(s => s.FechaRegistro >= desde.Value);
        if (hasta.HasValue)
            query = query.Where(s => s.FechaRegistro <= hasta.Value.Date.AddDays(1));

        var solicitudes = await query
            .OrderByDescending(s => s.FechaRegistro)
            .Select(s => new
            {
                Id = s.Id,
                Numero = s.NumeroProceso,
                NumeroProceso = s.NumeroProceso,
                Tipo = s.Tipo,
                Modulo = s.Modulo,
                Origen = s.CarreraOrigen != null ? s.CarreraOrigen.Nombre : "Origen",
                Destino = s.CarreraDestino != null ? s.CarreraDestino.Nombre : "Destino",
                CarreraOrigenId = s.CarreraOrigenId,
                CarreraDestinoId = s.CarreraDestinoId,
                UniversidadOrigen = s.UniversidadOrigen,
                UniversidadDestino = s.UniversidadDestino,
                FechaRegistro = s.FechaRegistro,
                Estado = s.Estado,
                Editable = s.Estado == EstadoSolicitud.REGISTRADA
            })
            .ToListAsync();

        return Ok(solicitudes);
    }

    /// <summary>Detalle de una solicitud propia (o de cualquiera si es Coordinador/Administrador).</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> ObtenerSolicitud(int id)
    {
        var detalle = await ObtenerDetalleInternoAsync(id);
        if (detalle is null)
            return NotFound(new { error = "Solicitud no encontrada." });

        var correo = CorreoActual();
        if (!string.IsNullOrEmpty(correo)
            && !string.Equals(correo, detalle.EstudianteCorreo, StringComparison.OrdinalIgnoreCase)
            && !EsCoordinadorOAdministrador())
            return StatusCode(StatusCodes.Status403Forbidden, new { mensaje = "No puede consultar una solicitud de otro estudiante." });

        return Ok(detalle);
    }

    [HttpGet("{id:int}/historial")]
    public async Task<IActionResult> ObtenerHistorial(int id)
    {
        var existe = await _db.Solicitudes.AnyAsync(s => s.Id == id);
        if (!existe)
            return NotFound(new { error = "Solicitud no encontrada." });

        var historial = await _db.HistorialEstados
            .Where(h => h.SolicitudId == id)
            .OrderBy(h => h.FechaCambio)
            .Select(h => new
            {
                h.Id,
                h.EstadoAnterior,
                h.EstadoNuevo,
                Usuario = h.UsuarioCorreo,
                Fecha = h.FechaCambio
            })
            .ToListAsync();

        return Ok(historial);
    }

    // --- ENDPOINT PARA EL COORDINADOR (LISTA) ---
    [HttpGet("internal/solicitudes/pendientes")]
    public async Task<IActionResult> ObtenerPendientes()
    {
        var pendientes = await _db.Solicitudes
            .AsNoTracking()
            .Include(s => s.CarreraOrigen)
            .Include(s => s.CarreraDestino)
            .Where(s => s.Estado == EstadoSolicitud.PENDIENTE_REVISION)
            .OrderBy(s => s.FechaRegistro)
            .Select(s => new
            {
                Id = s.Id,
                NumeroProceso = s.NumeroProceso,
                FechaRegistro = s.FechaRegistro,
                EstudianteCorreo = s.EstudianteCorreo,
                Tipo = s.Tipo,
                Modulo = s.Modulo,
                CarreraOrigen = s.CarreraOrigen != null ? s.CarreraOrigen.Nombre : "Origen",
                CarreraDestino = s.CarreraDestino != null ? s.CarreraDestino.Nombre : "Destino"
            })
            .ToListAsync();

        return Ok(pendientes);
    }

    // --- ENDPOINT PARA BUSCAR POR IDS (DETALLE DEL COORDINADOR) ---
    [HttpGet("internal/solicitudes")]
    public async Task<IActionResult> ObtenerPorIds([FromQuery] string ids)
    {
        if (string.IsNullOrEmpty(ids))
            return Ok(new List<object>());

        var idList = ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var n) ? n : 0)
            .Where(n => n > 0)
            .ToList();

        var solicitudes = await _db.Solicitudes
            .AsNoTracking()
            .Include(s => s.CarreraOrigen)
            .Include(s => s.CarreraDestino)
            .Where(s => idList.Contains(s.Id))
            .Select(s => new
            {
                Id = s.Id,
                NumeroProceso = s.NumeroProceso,
                FechaRegistro = s.FechaRegistro,
                EstudianteCorreo = s.EstudianteCorreo,
                Tipo = s.Tipo,
                Modulo = s.Modulo,
                UniversidadOrigen = s.UniversidadOrigen,
                UniversidadDestino = s.UniversidadDestino,
                CarreraOrigen = s.CarreraOrigen != null ? s.CarreraOrigen.Nombre : "Origen",
                CarreraDestino = s.CarreraDestino != null ? s.CarreraDestino.Nombre : "Destino",
                CarreraOrigenId = s.CarreraOrigenId,
                CarreraDestinoId = s.CarreraDestinoId,
                Estado = s.Estado
            })
            .ToListAsync();

        return Ok(solicitudes);
    }

    /// <summary>Detalle completo de una solicitud para los otros servicios (sin Gateway).</summary>
    [HttpGet("internal/solicitudes/{id:int}")]
    public async Task<IActionResult> ObtenerDetalle(int id)
    {
        var detalle = await ObtenerDetalleInternoAsync(id);
        return detalle is null
            ? NotFound(new { mensaje = "Solicitud no encontrada." })
            : Ok(detalle);
    }

    /// <summary>Cambia el estado de una solicitud y deja el cambio en el historial (uso interno).</summary>
    [HttpPut("internal/solicitudes/{id:int}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] CambiarEstadoRequest request)
    {
        var solicitud = await _db.Solicitudes.FirstOrDefaultAsync(s => s.Id == id);
        if (solicitud is null)
            return NotFound(new { mensaje = "Solicitud no encontrada." });

        var estadosValidos = new[]
        {
            EstadoSolicitud.REGISTRADA, EstadoSolicitud.PENDIENTE_REVISION,
            EstadoSolicitud.RESUELTA, EstadoSolicitud.APROBADA, EstadoSolicitud.RECHAZADA
        };
        if (!estadosValidos.Contains(request.Estado))
            return BadRequest(new { mensaje = "Estado no reconocido." });

        if (solicitud.Estado != request.Estado)
        {
            _db.HistorialEstados.Add(new HistorialEstado
            {
                SolicitudId = solicitud.Id,
                EstadoAnterior = solicitud.Estado,
                EstadoNuevo = request.Estado,
                UsuarioCorreo = string.IsNullOrWhiteSpace(request.Usuario) ? "sistema" : request.Usuario,
                FechaCambio = DateTime.UtcNow
            });
            solicitud.Estado = request.Estado;
            await _db.SaveChangesAsync();
        }

        return Ok(new { id = solicitud.Id, numeroProceso = solicitud.NumeroProceso, estado = solicitud.Estado });
    }

    /// <summary>Registra un cambio (por ejemplo una corrección) en el historial de la solicitud (uso interno).</summary>
    [HttpPost("internal/solicitudes/{id:int}/historial")]
    public async Task<IActionResult> RegistrarHistorial(int id, [FromBody] RegistrarHistorialRequest request)
    {
        var solicitud = await _db.Solicitudes.FirstOrDefaultAsync(s => s.Id == id);
        if (solicitud is null)
            return NotFound(new { mensaje = "Solicitud no encontrada." });

        _db.HistorialEstados.Add(new HistorialEstado
        {
            SolicitudId = solicitud.Id,
            EstadoAnterior = solicitud.Estado,
            EstadoNuevo = solicitud.Estado,
            UsuarioCorreo = string.IsNullOrWhiteSpace(request.Usuario) ? "sistema" : request.Usuario,
            FechaCambio = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        return Ok(new { id = solicitud.Id, estado = solicitud.Estado });
    }

    private async Task<string?> ValidarRequestAsync(SolicitudRequest request)
    {
        if (request.CarreraOrigenId == 0 || request.CarreraDestinoId == 0)
            return "Faltan campos obligatorios.";

        if (request.CarreraOrigenId == request.CarreraDestinoId)
            return "La carrera de origen y destino no pueden ser iguales.";

        if (request.AsignaturasIds == null || request.AsignaturasIds.Count == 0)
            return "Debe seleccionar al menos una asignatura.";

        var modulo = NormalizarModulo(request.Modulo);
        if (modulo == "EXTERNO")
        {
            if (string.IsNullOrWhiteSpace(request.UniversidadOrigen) || string.IsNullOrWhiteSpace(request.UniversidadDestino))
                return "En el módulo externo debe indicar la universidad de origen y la de destino.";
        }

        var carrerasExisten = await _db.Carreras.CountAsync(c =>
            c.Id == request.CarreraOrigenId || c.Id == request.CarreraDestinoId);
        if (carrerasExisten < 2)
            return "La carrera de origen o destino no existe.";

        var ids = request.AsignaturasIds.Distinct().ToList();
        var asignaturasExisten = await _db.Asignaturas.CountAsync(a => ids.Contains(a.Id));
        if (asignaturasExisten != ids.Count)
            return "Una o más asignaturas no existen en el catálogo.";

        return null;
    }

    private async Task<SolicitudDetalleDto?> ObtenerDetalleInternoAsync(int id)
    {
        var s = await _db.Solicitudes
            .AsNoTracking()
            .Include(x => x.CarreraOrigen)
            .Include(x => x.CarreraDestino)
            .Include(x => x.Asignaturas)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (s is null) return null;

        var ids = s.Asignaturas.Select(a => a.AsignaturaId).ToList();
        var asignaturas = await _db.Asignaturas.AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .ToListAsync();

        return new SolicitudDetalleDto
        {
            Id = s.Id,
            NumeroProceso = s.NumeroProceso,
            Tipo = s.Tipo,
            Modulo = s.Modulo,
            CarreraOrigenId = s.CarreraOrigenId,
            CarreraOrigen = s.CarreraOrigen?.Nombre ?? "Origen",
            CarreraDestinoId = s.CarreraDestinoId,
            CarreraDestino = s.CarreraDestino?.Nombre ?? "Destino",
            UniversidadOrigen = s.UniversidadOrigen,
            UniversidadDestino = s.UniversidadDestino,
            EstudianteCorreo = s.EstudianteCorreo,
            FechaRegistro = s.FechaRegistro,
            Estado = s.Estado,
            Editable = EstadoSolicitud.EsEditable(s.Estado),
            Asignaturas = s.Asignaturas.Select(sa =>
            {
                var a = asignaturas.FirstOrDefault(x => x.Id == sa.AsignaturaId);
                return new SolicitudAsignaturaDto
                {
                    AsignaturaId = sa.AsignaturaId,
                    Nombre = a?.Nombre ?? $"Asignatura {sa.AsignaturaId}",
                    Nivel = a?.Nivel ?? string.Empty,
                    Creditos = a?.Creditos ?? 0,
                    Horas = a?.Horas ?? 0
                };
            }).ToList()
        };
    }

    private static string NormalizarModulo(string? modulo) =>
        string.Equals(modulo, "EXTERNO", StringComparison.OrdinalIgnoreCase) ? "EXTERNO" : "INTERNO";

    private string? CorreoActual() =>
        User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");

    private bool EsCoordinadorOAdministrador() =>
        User.FindFirstValue("rol") is "COORDINADOR" or "ADMINISTRADOR";
}

public class CambiarEstadoRequest
{
    public string Estado { get; set; } = string.Empty;
    public string? Usuario { get; set; }
}

public class RegistrarHistorialRequest
{
    public string? Usuario { get; set; }
}
