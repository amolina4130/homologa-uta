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

        await using var tx = await _db.Database.BeginTransactionAsync();
        var siguienteNumero = await ObtenerSiguienteNumeroProcesoAsync();

        var solicitud = new Solicitud
        {
            Tipo = "Homologación",
            CarreraOrigenId = request.CarreraOrigenId,
            CarreraDestinoId = request.CarreraDestinoId,
            EstudianteCorreo = correo,
            // El Comité no ingresa al titular como usuario: el nombre y la cédula se toman
            // del récord académico. Estos valores son solo el estado inicial.
            EstudianteNombre = string.IsNullOrWhiteSpace(request.EstudianteNombre)
                ? Solicitud.NombreSinIdentificar
                : request.EstudianteNombre.Trim(),
            EstudianteCedula = string.IsNullOrWhiteSpace(request.EstudianteCedula)
                ? Solicitud.CedulaSinIdentificar
                : request.EstudianteCedula.Trim(),
            Modulo = modulo,
            UniversidadOrigen = modulo == "EXTERNO" ? request.UniversidadOrigen?.Trim() : null,
            UniversidadDestino = modulo == "EXTERNO" ? request.UniversidadDestino?.Trim() : null,
            Estado = EstadoSolicitud.REGISTRADA,
            NumeroProceso = siguienteNumero
        };

        foreach (var asigId in request.AsignaturasIds.Distinct())
            solicitud.Asignaturas.Add(new SolicitudAsignatura { AsignaturaId = asigId });

        _db.Solicitudes.Add(solicitud);
        await _db.SaveChangesAsync();

        _db.HistorialEstados.Add(new HistorialEstado
        {
            SolicitudId = solicitud.Id,
            EstadoAnterior = null,
            EstadoNuevo = solicitud.Estado,
            UsuarioCorreo = correo,
            FechaCambio = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

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
                EstudianteNombre = s.EstudianteNombre,
                EstudianteCedula = s.EstudianteCedula,
                ComiteCorreo = s.EstudianteCorreo,
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
            EstudianteNombre = solicitud.EstudianteNombre,
            EstudianteCedula = solicitud.EstudianteCedula,
            ComiteCorreo = solicitud.EstudianteCorreo,
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
        solicitud.EstudianteNombre = string.IsNullOrWhiteSpace(request.EstudianteNombre)
            ? Solicitud.NombreSinIdentificar
            : request.EstudianteNombre.Trim();
        solicitud.EstudianteCedula = string.IsNullOrWhiteSpace(request.EstudianteCedula)
            ? Solicitud.CedulaSinIdentificar
            : request.EstudianteCedula.Trim();
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
            FechaCambio = DateTime.UtcNow,
            Detalle = "Solicitud editada"
        });

        await _db.SaveChangesAsync();

        return Ok(new
        {
            id = solicitud.Id,
            numeroProceso = solicitud.NumeroProceso,
            mensaje = "Solicitud actualizada correctamente."
        });
    }

    /// <summary>
    /// Corrige el nombre y la cédula del titular del récord. No cambia el estado de la solicitud
    /// ni genera informes: solo deja el dato listo para la revisión del Docente y el informe final.
    /// </summary>
    [HttpPost("{id:int}/titular")]
    public async Task<IActionResult> ActualizarTitular(int id, [FromBody] ActualizarTitularRequest? request)
    {
        var solicitud = await _db.Solicitudes.FirstOrDefaultAsync(s => s.Id == id);
        if (solicitud is null)
            return NotFound(new { error = "Solicitud no encontrada." });

        if (!PuedeGestionar(solicitud.EstudianteCorreo))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "No puede modificar el titular de esta solicitud." });

        var nombre = request?.Nombre?.Trim();
        var cedula = request?.Cedula?.Trim();
        var cambio = false;

        if (!string.IsNullOrWhiteSpace(nombre) && nombre.Length <= 200 &&
            !string.Equals(solicitud.EstudianteNombre, nombre, StringComparison.Ordinal))
        {
            solicitud.EstudianteNombre = nombre;
            cambio = true;
        }

        if (!string.IsNullOrWhiteSpace(cedula) && cedula.Length <= 50 &&
            !string.Equals(solicitud.EstudianteCedula, cedula, StringComparison.Ordinal))
        {
            solicitud.EstudianteCedula = cedula;
            cambio = true;
        }

        if (cambio)
        {
            _db.HistorialEstados.Add(new HistorialEstado
            {
                SolicitudId = id,
                EstadoAnterior = solicitud.Estado,
                EstadoNuevo = solicitud.Estado,
                UsuarioCorreo = ObtenerCorreo(),
                FechaCambio = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        return Ok(new
        {
            id,
            numeroProceso = solicitud.NumeroProceso,
            estudianteNombre = solicitud.EstudianteNombre,
            estudianteCedula = solicitud.EstudianteCedula,
            estado = solicitud.Estado,
            mensaje = cambio
                ? "Datos del titular actualizados. El estado de la solicitud no cambió."
                : "No hay cambios en los datos del titular."
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
                EstudianteNombre = s.EstudianteNombre,
                EstudianteCedula = s.EstudianteCedula,
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
                EstudianteNombre = s.EstudianteNombre,
                EstudianteCedula = s.EstudianteCedula,
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

    private bool PuedeGestionar(string propietario)
    {
        var correo = ObtenerCorreo();
        var rolesGestores = new[] { "COORDINADOR", "COMITE", "ADMINISTRADOR" };
        return string.Equals(propietario, correo, StringComparison.OrdinalIgnoreCase)
               || rolesGestores.Any(r => User.FindFirstValue("rol")
                   ?.Equals(r, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static string NormalizarModulo(string? modulo) =>
        string.Equals(modulo?.Trim(), "EXTERNO", StringComparison.OrdinalIgnoreCase)
            ? "EXTERNO"
            : "INTERNO";

    private static string? ValidarRequest(SolicitudRequest request)
    {
        if (request is null)
            return "Los datos de la solicitud son obligatorios.";

        // No se exige nombre/cédula al registrar: el titular del trámite se obtiene
        // del récord académico que entrega el Comité y se guarda en la solicitud.
        var cedula = request.EstudianteCedula?.Trim();
        if (!string.IsNullOrWhiteSpace(cedula) &&
            (cedula.Length != 10 || !cedula.All(char.IsDigit)))
        {
            return "La cédula del estudiante debe tener exactamente 10 dígitos.";
        }

        if (request.CarreraOrigenId == 0 || request.CarreraDestinoId == 0)
            return "Faltan campos obligatorios.";

        if (request.CarreraOrigenId == request.CarreraDestinoId)
            return "La carrera de origen y destino no pueden ser iguales.";

        if (request.AsignaturasIds == null || !request.AsignaturasIds.Any())
            return "Debe seleccionar al menos una asignatura.";

        if ((request.UniversidadOrigen?.Trim().Length ?? 0) > 150 ||
            (request.UniversidadDestino?.Trim().Length ?? 0) > 150)
        {
            return "El nombre de la universidad no puede superar los 150 caracteres.";
        }

        if (NormalizarModulo(request.Modulo) == "EXTERNO" &&
            (string.IsNullOrWhiteSpace(request.UniversidadOrigen) ||
             string.IsNullOrWhiteSpace(request.UniversidadDestino)))
        {
            return "Para el agente externo debe indicar la universidad de origen y la universidad de destino.";
        }

        return null;
    }

    private async Task<string> ObtenerSiguienteNumeroProcesoAsync()
    {
        // Secuencia legible y consecutiva: SOL-0001, SOL-0002, ...
        // No depende del Id IDENTITY (puede saltar por reinicios de SQL Server).
        int maximo;
        if (_db.Database.IsRelational())
        {
            maximo = await _db.Database
                .SqlQueryRaw<int>(@"
                    SELECT ISNULL(MAX(CAST(SUBSTRING(NumeroProceso, 5, 30) AS int)), 0) AS [Value]
                    FROM Solicitudes WITH (UPDLOCK, HOLDLOCK)
                    WHERE NumeroProceso LIKE 'SOL-[0-9][0-9][0-9][0-9]%'")
                .SingleAsync();
        }
        else
        {
            var numeros = await _db.Solicitudes
                .Select(s => s.NumeroProceso)
                .ToListAsync();

            maximo = numeros
                .Select(n =>
                {
                    if (string.IsNullOrWhiteSpace(n) || !n.StartsWith("SOL-", StringComparison.OrdinalIgnoreCase))
                        return 0;
                    var parte = n.Substring(4);
                    var digitos = new string(parte.TakeWhile(char.IsDigit).ToArray());
                    return int.TryParse(digitos, out var valor) ? valor : 0;
                })
                .DefaultIfEmpty(0)
                .Max();
        }

        return $"SOL-{maximo + 1:D4}";
    }
}
