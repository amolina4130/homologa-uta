using HomologaUTA.Solicitudes.Data;
using HomologaUTA.Solicitudes.DTOs;
using HomologaUTA.Solicitudes.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HomologaUTA.Solicitudes.Controllers;

[ApiController]
[Route("solicitudes")]
// El Gateway ya protege con JWT; aquí se leen los claims para validar al dueño.
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
        var error = ValidarRequest(request);
        if (error is not null)
            return BadRequest(new { error });

        var modulo = NormalizarModulo(request.Modulo);
        var correo = ObtenerCorreo();

        var solicitud = new Solicitud
        {
            Tipo = "Homologación",
            CarreraOrigenId = request.CarreraOrigenId,
            CarreraDestinoId = request.CarreraDestinoId,
            EstudianteCorreo = correo,
            Modulo = modulo,
            UniversidadOrigen = modulo == "EXTERNO" ? request.UniversidadOrigen?.Trim() : null,
            UniversidadDestino = modulo == "EXTERNO" ? request.UniversidadDestino?.Trim() : null,
            Estado = EstadoSolicitud.REGISTRADA
        };

        foreach (var asigId in request.AsignaturasIds.Distinct())
            solicitud.Asignaturas.Add(new SolicitudAsignatura { AsignaturaId = asigId });

        _db.Solicitudes.Add(solicitud);
        await _db.SaveChangesAsync();

        // El número de proceso usa el Id ya generado: SOL-0004 (HU-16).
        solicitud.NumeroProceso = $"SOL-{solicitud.Id:D4}";

        _db.HistorialEstados.Add(new HistorialEstado
        {
            SolicitudId = solicitud.Id,
            EstadoAnterior = null,
            EstadoNuevo = solicitud.Estado,
            UsuarioCorreo = correo,
            FechaCambio = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        return Created($"/solicitudes/{solicitud.Id}", new
        {
            id = solicitud.Id,
            numeroProceso = solicitud.NumeroProceso,
            mensaje = "Solicitud registrada exitosamente."
        });
    }

    [HttpGet("mias")]
    public async Task<IActionResult> ObtenerMisSolicitudes(
        [FromQuery] string? estado,
        [FromQuery] int? carreraId,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta)
    {
        var correo = ObtenerCorreo();

        var consulta = _db.Solicitudes
            .AsNoTracking()
            .Include(s => s.CarreraOrigen)
            .Include(s => s.CarreraDestino)
            .Where(s => s.EstudianteCorreo == correo);

        if (!string.IsNullOrWhiteSpace(estado))
        {
            var valor = estado.Trim().ToUpperInvariant();
            consulta = consulta.Where(s => s.Estado == valor);
        }

        if (carreraId is > 0)
            consulta = consulta.Where(s => s.CarreraOrigenId == carreraId || s.CarreraDestinoId == carreraId);

        if (desde.HasValue)
            consulta = consulta.Where(s => s.FechaRegistro >= desde.Value.Date);

        if (hasta.HasValue)
            consulta = consulta.Where(s => s.FechaRegistro < hasta.Value.Date.AddDays(1));

        var solicitudes = await consulta
            .OrderByDescending(s => s.FechaRegistro)
            .Select(s => new
            {
                Id = s.Id,
                NumeroProceso = string.IsNullOrEmpty(s.NumeroProceso)
                    ? "SOL-" + s.Id.ToString("D4")
                    : s.NumeroProceso,
                Numero = string.IsNullOrEmpty(s.NumeroProceso)
                    ? "SOL-" + s.Id.ToString("D4")
                    : s.NumeroProceso,
                Tipo = s.Tipo,
                Origen = s.CarreraOrigen != null ? s.CarreraOrigen.Nombre : "Origen",
                Destino = s.CarreraDestino != null ? s.CarreraDestino.Nombre : "Destino",
                CarreraOrigenId = s.CarreraOrigenId,
                CarreraDestinoId = s.CarreraDestinoId,
                FechaRegistro = s.FechaRegistro,
                Estado = s.Estado,
                Modulo = s.Modulo,
                UniversidadOrigen = s.UniversidadOrigen,
                UniversidadDestino = s.UniversidadDestino,
                PuedeEditar = s.Estado == EstadoSolicitud.REGISTRADA
            })
            .ToListAsync();

        return Ok(solicitudes);
    }

    /// <summary>Datos de una solicitud propia (incluye asignaturas y si se puede editar).</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> ObtenerSolicitud(int id)
    {
        var correo = ObtenerCorreo();

        var solicitud = await _db.Solicitudes
            .AsNoTracking()
            .Include(s => s.CarreraOrigen)
            .Include(s => s.CarreraDestino)
            .Include(s => s.Asignaturas)
                .ThenInclude(a => a.Asignatura)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud is null)
            return NotFound(new { error = "Solicitud no encontrada." });

        if (!PuedeVer(solicitud.EstudianteCorreo, correo))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "No tiene permiso para ver esta solicitud." });

        return Ok(new
        {
            Id = solicitud.Id,
            NumeroProceso = string.IsNullOrEmpty(solicitud.NumeroProceso)
                ? $"SOL-{solicitud.Id:D4}"
                : solicitud.NumeroProceso,
            Numero = string.IsNullOrEmpty(solicitud.NumeroProceso)
                ? $"SOL-{solicitud.Id:D4}"
                : solicitud.NumeroProceso,
            Tipo = solicitud.Tipo,
            CarreraOrigenId = solicitud.CarreraOrigenId,
            CarreraDestinoId = solicitud.CarreraDestinoId,
            Origen = solicitud.CarreraOrigen?.Nombre ?? "Origen",
            Destino = solicitud.CarreraDestino?.Nombre ?? "Destino",
            FechaRegistro = solicitud.FechaRegistro,
            Estado = solicitud.Estado,
            Modulo = solicitud.Modulo,
            UniversidadOrigen = solicitud.UniversidadOrigen,
            UniversidadDestino = solicitud.UniversidadDestino,
            PuedeEditar = solicitud.Estado == EstadoSolicitud.REGISTRADA,
            Asignaturas = solicitud.Asignaturas
                .OrderBy(a => a.Id)
                .Select(a => new
                {
                    id = a.Id,
                    asignaturaId = a.AsignaturaId,
                    nombre = a.Asignatura?.Nombre ?? string.Empty,
                    nivel = a.Asignatura?.Nivel ?? string.Empty,
                    creditos = a.Asignatura?.Creditos ?? 0,
                    horas = a.Asignatura?.Horas ?? 0
                })
        });
    }

    /// <summary>Edita una solicitud mientras siga REGISTRADA (HU-16).</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> EditarSolicitud(int id, [FromBody] SolicitudRequest request)
    {
        var error = ValidarRequest(request);
        if (error is not null)
            return BadRequest(new { error });

        var solicitud = await _db.Solicitudes
            .Include(s => s.Asignaturas)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud is null)
            return NotFound(new { error = "Solicitud no encontrada." });

        var correo = ObtenerCorreo();
        if (!PuedeVer(solicitud.EstudianteCorreo, correo))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "No puede editar la solicitud de otro estudiante." });

        if (solicitud.Estado != EstadoSolicitud.REGISTRADA)
            return Conflict(new
            {
                mensaje = "La solicitud ya pasó a revisión y no se puede editar. Solo se edita mientras está REGISTRADA."
            });

        var modulo = NormalizarModulo(request.Modulo);
        solicitud.CarreraOrigenId = request.CarreraOrigenId;
        solicitud.CarreraDestinoId = request.CarreraDestinoId;
        solicitud.Modulo = modulo;
        solicitud.UniversidadOrigen = modulo == "EXTERNO" ? request.UniversidadOrigen?.Trim() : null;
        solicitud.UniversidadDestino = modulo == "EXTERNO" ? request.UniversidadDestino?.Trim() : null;

        _db.SolicitudAsignaturas.RemoveRange(solicitud.Asignaturas);
        solicitud.Asignaturas.Clear();
        foreach (var asigId in request.AsignaturasIds.Distinct())
            solicitud.Asignaturas.Add(new SolicitudAsignatura { SolicitudId = id, AsignaturaId = asigId });

        _db.HistorialEstados.Add(new HistorialEstado
        {
            SolicitudId = id,
            EstadoAnterior = solicitud.Estado,
            EstadoNuevo = solicitud.Estado,
            UsuarioCorreo = correo,
            FechaCambio = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        return Ok(new
        {
            id = solicitud.Id,
            numeroProceso = solicitud.NumeroProceso,
            mensaje = "Solicitud actualizada correctamente."
        });
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

        var idList = ids.Split(',')
            .Select(x => int.TryParse(x, out var n) ? n : 0)
            .Where(n => n > 0)
            .ToList();

        var solicitudes = await _db.Solicitudes
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
                CarreraOrigen = s.CarreraOrigen != null ? s.CarreraOrigen.Nombre : "Origen",
                CarreraDestino = s.CarreraDestino != null ? s.CarreraDestino.Nombre : "Destino",
                Estado = s.Estado
            })
            .ToListAsync();

        return Ok(solicitudes);
    }

    private string ObtenerCorreo() =>
        User.FindFirstValue(ClaimTypes.Email)
        ?? User.FindFirstValue("email")
        ?? "estudiante@uta.edu.ec";

    private bool PuedeVer(string propietario, string correo) =>
        string.Equals(propietario, correo, StringComparison.OrdinalIgnoreCase)
        || User.FindFirstValue("rol")?.Equals("ADMINISTRADOR", StringComparison.OrdinalIgnoreCase) == true;

    private static string NormalizarModulo(string? modulo) =>
        string.Equals(modulo?.Trim(), "EXTERNO", StringComparison.OrdinalIgnoreCase)
            ? "EXTERNO"
            : "INTERNO";

    private static string? ValidarRequest(SolicitudRequest request)
    {
        if (request is null)
            return "Los datos de la solicitud son obligatorios.";

        if (request.CarreraOrigenId == 0 || request.CarreraDestinoId == 0)
            return "Faltan campos obligatorios.";

        if (request.CarreraOrigenId == request.CarreraDestinoId)
            return "La carrera de origen y destino no pueden ser iguales.";

        if (request.AsignaturasIds == null || !request.AsignaturasIds.Any())
            return "Debe seleccionar al menos una asignatura.";

        if (NormalizarModulo(request.Modulo) == "EXTERNO" &&
            (string.IsNullOrWhiteSpace(request.UniversidadOrigen) ||
             string.IsNullOrWhiteSpace(request.UniversidadDestino)))
        {
            return "Para el agente externo debe indicar la universidad de origen y la universidad de destino.";
        }

        return null;
    }
}
