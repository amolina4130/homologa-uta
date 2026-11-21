using System.Security.Claims;
using System.Text;
using System.Text.Json;
using HomologaUTA.Solicitudes.Data;
using HomologaUTA.Solicitudes.DTOs;
using HomologaUTA.Solicitudes.Models;
using HomologaUTA.Solicitudes.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Solicitudes.Controllers;

[ApiController]
[Route("solicitudes")]
// [Authorize] <--- Lo dejamos comentado para tus pruebas
public class SolicitudesController : ControllerBase
{
    private const string CorreoPorDefecto = "estudiante@uta.edu.ec";

    private readonly SolicitudesDbContext _db;

    private const int MaxUniversidad = 150;

    // El contrato pide { mensaje }; se deja también "error" porque el frontend actual lo lee.
    private BadRequestObjectResult ErrorUniversidad(string texto) =>
        BadRequest(new { mensaje = texto, error = texto });

    public SolicitudesController(SolicitudesDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> CrearSolicitud([FromBody] SolicitudRequest request)
    {
        var error = Validar(request, out var modulo);
        if (error is not null)
            return error;

        var correo = ObtenerCorreo();

        var solicitud = new Solicitud
        {
            Tipo = "Homologación",
            // Número provisional: se reemplaza por el definitivo apenas la base asigna el Id.
            NumeroProceso = "TMP-" + Guid.NewGuid().ToString("N")[..12],
            CarreraOrigenId = request.CarreraOrigenId,
            CarreraDestinoId = request.CarreraDestinoId,
            EstudianteCorreo = correo,
            Modulo = modulo,
            UniversidadOrigen = modulo == "EXTERNO" ? request.UniversidadOrigen?.Trim() : null,
            UniversidadDestino = modulo == "EXTERNO" ? request.UniversidadDestino?.Trim() : null,
            // La solicitud nace REGISTRADA. Homologación la pasa a PENDIENTE_REVISION
            // cuando el agente termina el análisis (así el Coordinador nunca ve una
            // solicitud sin propuesta).
            Estado = EstadoSolicitud.REGISTRADA
        };

        foreach (var asigId in request.AsignaturasIds.Distinct())
        {
            solicitud.Asignaturas.Add(new SolicitudAsignatura { AsignaturaId = asigId });
        }

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

        // Con el Id ya asignado se arma el número de proceso definitivo (único porque el Id lo es).
        solicitud.NumeroProceso = GeneradorNumeroProceso.Formatear(solicitud.Id);
        await _db.SaveChangesAsync();

        return Created($"/solicitudes/{solicitud.Id}", new
        {
            id = solicitud.Id,
            numeroProceso = solicitud.NumeroProceso,
            mensaje = "Solicitud registrada exitosamente.",
            modulo = solicitud.Modulo,
            universidadOrigen = solicitud.UniversidadOrigen,
            universidadDestino = solicitud.UniversidadDestino
        });
    }

    /// <summary>Edita una solicitud propia mientras siga REGISTRADA (antes de pasar a revisión).</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> EditarSolicitud(int id, [FromBody] SolicitudRequest request)
    {
        var solicitud = await _db.Solicitudes
            .Include(s => s.Asignaturas)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud is null)
            return NotFound(new { mensaje = "Solicitud no encontrada.", error = "Solicitud no encontrada." });

        var correo = ObtenerCorreo();
        if (!string.Equals(solicitud.EstudianteCorreo, correo, StringComparison.OrdinalIgnoreCase))
        {
            const string texto = "No puede editar la solicitud de otro estudiante.";
            return StatusCode(StatusCodes.Status403Forbidden, new { mensaje = texto, error = texto });
        }

        if (solicitud.Estado != EstadoSolicitud.REGISTRADA)
        {
            var texto = $"La solicitud ya está en estado {solicitud.Estado} y no se puede editar.";
            return Conflict(new { mensaje = texto, error = texto });
        }

        var errorEntrada = Validar(request, out var modulo);
        if (errorEntrada is not null)
            return errorEntrada;

        solicitud.CarreraOrigenId = request.CarreraOrigenId;
        solicitud.CarreraDestinoId = request.CarreraDestinoId;
        solicitud.Modulo = modulo;
        solicitud.UniversidadOrigen = request.UniversidadOrigen?.Trim();
        solicitud.UniversidadDestino = request.UniversidadDestino?.Trim();

        _db.SolicitudAsignaturas.RemoveRange(solicitud.Asignaturas.ToList());
        solicitud.Asignaturas.Clear();
        foreach (var asigId in request.AsignaturasIds.Distinct())
        {
            solicitud.Asignaturas.Add(new SolicitudAsignatura { AsignaturaId = asigId });
        }

        // La edición no cambia el estado: queda como un movimiento con el mismo estado y una nota.
        _db.HistorialEstados.Add(new HistorialEstado
        {
            SolicitudId = solicitud.Id,
            EstadoAnterior = solicitud.Estado,
            EstadoNuevo = solicitud.Estado,
            UsuarioCorreo = correo,
            FechaCambio = DateTime.UtcNow,
            Detalle = "Solicitud editada por el estudiante."
        });

        await _db.SaveChangesAsync();

        return Ok(await ArmarDetalleAsync(solicitud));
    }

    [HttpGet("mias")]
    public async Task<IActionResult> ObtenerMisSolicitudes()
    {
        var correo = ObtenerCorreo();

        var solicitudes = await _db.Solicitudes
            .Include(s => s.CarreraOrigen)
            .Include(s => s.CarreraDestino)
            .Where(s => s.EstudianteCorreo == correo)
            .OrderByDescending(s => s.FechaRegistro)
            .Select(s => new 
            {
                Id = s.Id,
                Numero = s.NumeroProceso,
                NumeroProceso = s.NumeroProceso,
                Tipo = s.Tipo,
                Origen = s.CarreraOrigen != null ? s.CarreraOrigen.Nombre : "Origen",
                Destino = s.CarreraDestino != null ? s.CarreraDestino.Nombre : "Destino",
                FechaRegistro = s.FechaRegistro.ToString("yyyy-MM-dd"),
                Estado = s.Estado,
                Modulo = s.Modulo,
                UniversidadOrigen = s.UniversidadOrigen,
                UniversidadDestino = s.UniversidadDestino
            })
            .ToListAsync();

        return Ok(solicitudes);
    }

    /// <summary>Datos de una solicitud propia (incluye el módulo de análisis).</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> ObtenerSolicitud(int id)
    {
        var correo = ObtenerCorreo();

        var solicitud = await _db.Solicitudes
            .AsNoTracking()
            .Include(s => s.Asignaturas)
            .FirstOrDefaultAsync(s => s.Id == id && s.EstudianteCorreo == correo);

        if (solicitud is null)
            return NotFound(new { error = "Solicitud no encontrada." });

        return Ok(await ArmarDetalleAsync(solicitud));
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
                Fecha = h.FechaCambio,
                h.Detalle
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

        var idList = ids.Split(',').Select(int.Parse).ToList();

        var solicitudes = await _db.Solicitudes
            .Include(s => s.CarreraOrigen)
            .Include(s => s.CarreraDestino)
            .Where(s => idList.Contains(s.Id))
            .Select(s => new 
            {
                Id = s.Id,
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

    // ---------------------------------------------------------------
    // Ayudas
    // ---------------------------------------------------------------

    // Reglas comunes de crear (POST) y editar (PUT). Devuelve el error, o null si todo está bien.
    private IActionResult? Validar(SolicitudRequest request, out string modulo)
    {
        modulo = "INTERNO";

        if (request.CarreraOrigenId == 0 || request.CarreraDestinoId == 0)
            return BadRequest(new { mensaje = "Faltan campos obligatorios.", error = "Faltan campos obligatorios." });

        if (request.CarreraOrigenId == request.CarreraDestinoId)
            return BadRequest(new { mensaje = "La carrera de origen y destino no pueden ser iguales.", error = "La carrera de origen y destino no pueden ser iguales." });

        if (request.AsignaturasIds == null || !request.AsignaturasIds.Any())
            return BadRequest(new { mensaje = "Debe seleccionar al menos una asignatura.", error = "Debe seleccionar al menos una asignatura." });

        if (string.Equals(request.Modulo?.Trim(), "EXTERNO", StringComparison.OrdinalIgnoreCase))
        {
            modulo = "EXTERNO";

            if (string.IsNullOrWhiteSpace(request.UniversidadOrigen) ||
                string.IsNullOrWhiteSpace(request.UniversidadDestino))
            {
                return BadRequest(new
                {
                    mensaje = "Para el agente externo debe indicar la universidad de origen y la universidad de destino.",
                    error = "Para el agente externo debe indicar la universidad de origen y la universidad de destino."
                });
            }

            // Validación de longitud
            if (request.UniversidadOrigen.Trim().Length > MaxUniversidad || 
                request.UniversidadDestino.Trim().Length > MaxUniversidad)
            {
                return BadRequest(new
                {
                    mensaje = $"El nombre de la universidad no puede superar los {MaxUniversidad} caracteres.",
                    error = $"El nombre de la universidad no puede superar los {MaxUniversidad} caracteres."
                });
            }
        }

        return null;
    }
    private async Task<object> ArmarDetalleAsync(Solicitud s)
    {
        var nombres = await _db.Carreras
            .AsNoTracking()
            .Where(c => c.Id == s.CarreraOrigenId || c.Id == s.CarreraDestinoId)
            .ToDictionaryAsync(c => c.Id, c => c.Nombre);

        string Nombre(int carreraId, string porDefecto) =>
            nombres.TryGetValue(carreraId, out var n) ? n : porDefecto;

        return new
        {
            Id = s.Id,
            Numero = s.NumeroProceso,
            NumeroProceso = s.NumeroProceso,
            Tipo = s.Tipo,
            CarreraOrigenId = s.CarreraOrigenId,
            CarreraDestinoId = s.CarreraDestinoId,
            Origen = Nombre(s.CarreraOrigenId, "Origen"),
            Destino = Nombre(s.CarreraDestinoId, "Destino"),
            AsignaturasIds = s.Asignaturas.Select(a => a.AsignaturaId).OrderBy(x => x).ToList(),
            FechaRegistro = s.FechaRegistro,
            Estado = s.Estado,
            Modulo = s.Modulo,
            UniversidadOrigen = s.UniversidadOrigen,
            UniversidadDestino = s.UniversidadDestino
        };
    }

    // Solicitudes no valida el JWT (lo hace el Gateway): se lee el correo de los claims y, si no
    // hay, del contenido del token, igual que el controlador del Coordinador.
    private string ObtenerCorreo()
    {
        var usuario = HttpContext?.User;
        var delClaim = usuario?.FindFirstValue(ClaimTypes.Email) ?? usuario?.FindFirstValue("email");
        if (!string.IsNullOrWhiteSpace(delClaim))
            return delClaim;

        var cabecera = HttpContext?.Request.Headers.Authorization.ToString() ?? string.Empty;
        if (cabecera.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var partes = cabecera["Bearer ".Length..].Trim().Split('.');
            if (partes.Length == 3)
            {
                try
                {
                    var payload = partes[1].Replace('-', '+').Replace('_', '/');
                    payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

                    using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
                    if (doc.RootElement.TryGetProperty("email", out var valor) &&
                        valor.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(valor.GetString()))
                        return valor.GetString()!;
                }
                catch (Exception ex) when (ex is FormatException or JsonException)
                {
                    // Token ilegible: se usa el correo por defecto.
                }
            }
        }

        return CorreoPorDefecto;
    }
}
