using HomologaUTA.Solicitudes.Data;
using HomologaUTA.Solicitudes.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Solicitudes.Controllers;

/// <summary>
/// Consultas entre servicios. El Gateway no tiene ninguna ruta /internal, así que solo se alcanza
/// desde la red interna de Docker (o desde localhost en desarrollo).
/// </summary>
[ApiController]
[Route("internal/solicitudes")]
public class InternoController : ControllerBase
{
    private const int MaxIds = 200;
    private const string SinEquivalenteEnMallaDestino = "Sin equivalente en la malla destino";

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
                NumeroProceso = s.NumeroProceso,
                Tipo = s.Tipo,
                EstudianteCorreo = s.EstudianteCorreo,
                EstudianteNombre = s.EstudianteNombre,
                EstudianteCedula = s.EstudianteCedula,
                FechaRegistro = s.FechaRegistro,
                CarreraOrigenId = s.CarreraOrigenId,
                CarreraDestinoId = s.CarreraDestinoId,
                CarreraOrigen = s.CarreraOrigen.Nombre,
                CarreraDestino = s.CarreraDestino.Nombre,
                Estado = s.Estado,
                Modulo = s.Modulo,
                UniversidadOrigen = s.UniversidadOrigen,
                UniversidadDestino = s.UniversidadDestino,
                TotalAsignaturas = s.Asignaturas.Count
            })
            .ToListAsync();

        return Ok(solicitudes);
    }

    /// <summary>Solicitudes que están pendientes de revisión del Coordinador.</summary>
    [HttpGet("pendientes")]
    public async Task<IActionResult> ObtenerPendientes()
    {
        var pendientes = await _db.Solicitudes
            .Where(s => s.Estado == EstadoSolicitud.PENDIENTE_REVISION)
            .OrderBy(s => s.FechaRegistro)
            .Select(s => new SolicitudInternaDto
            {
                Id = s.Id,
                NumeroProceso = s.NumeroProceso,
                Tipo = s.Tipo,
                EstudianteCorreo = s.EstudianteCorreo,
                EstudianteNombre = s.EstudianteNombre,
                EstudianteCedula = s.EstudianteCedula,
                FechaRegistro = s.FechaRegistro,
                CarreraOrigenId = s.CarreraOrigenId,
                CarreraDestinoId = s.CarreraDestinoId,
                CarreraOrigen = s.CarreraOrigen.Nombre,
                CarreraDestino = s.CarreraDestino.Nombre,
                Estado = s.Estado,
                Modulo = s.Modulo,
                UniversidadOrigen = s.UniversidadOrigen,
                UniversidadDestino = s.UniversidadDestino,
                TotalAsignaturas = s.Asignaturas.Count
            })
            .ToListAsync();

        return Ok(pendientes);
    }

    /// <summary>
    /// Solicitud completa para el análisis: datos generales y asignaturas de origen con su
    /// equivalente en la carrera de destino. La equivalencia se busca por nombre o por una regla
    /// activa; si no existe, se marca el destino como "Sin equivalente en la malla destino" para
    /// que el análisis lo resuelva contra la memoria de la carrera destino.
    /// </summary>
    [HttpGet("{id:int}/completa")]
    public async Task<IActionResult> ObtenerCompleta(int id)
    {
        if (id <= 0)
            return BadRequest(new { error = "El id de la solicitud debe ser mayor que cero." });

        var solicitud = await _db.Solicitudes
            .AsNoTracking()
            .Include(s => s.CarreraOrigen)
            .Include(s => s.CarreraDestino)
            .Include(s => s.Asignaturas)
                .ThenInclude(sa => sa.Asignatura)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud is null)
            return NotFound(new { error = "Solicitud no encontrada." });

        // Catálogo de la carrera destino para buscar la asignatura equivalente por nombre.
        var catalogoDestino = await _db.Asignaturas
            .AsNoTracking()
            .Where(a => a.CarreraId == solicitud.CarreraDestinoId)
            .ToListAsync();

        // HU-14: las equivalencias activas se aplican antes que la coincidencia por nombre.
        var equivalencias = await _db.Equivalencias
            .AsNoTracking()
            .Where(e => e.Activa
                        && e.CarreraOrigenId == solicitud.CarreraOrigenId
                        && e.CarreraDestinoId == solicitud.CarreraDestinoId)
            .ToListAsync();

        // HU-13/HU-19: el contenido real viene del plan analítico cuando está cargado.
        var idsAsignaturas = solicitud.Asignaturas.Select(a => a.AsignaturaId)
            .Concat(catalogoDestino.Select(a => a.Id))
            .Distinct()
            .ToList();

        var planes = await _db.PlanesAnaliticos
            .AsNoTracking()
            .Where(p => idsAsignaturas.Contains(p.AsignaturaId))
            .ToDictionaryAsync(p => p.AsignaturaId, p => p.Contenido);

        var dto = new SolicitudCompletaInternaDto
        {
            Id = solicitud.Id,
            NumeroProceso = string.IsNullOrEmpty(solicitud.NumeroProceso)
                ? $"SOL-{solicitud.Id:D4}"
                : solicitud.NumeroProceso,
            Tipo = solicitud.Tipo,
            EstudianteCorreo = solicitud.EstudianteCorreo,
            EstudianteNombre = solicitud.EstudianteNombre,
            EstudianteCedula = solicitud.EstudianteCedula,
            FechaRegistro = solicitud.FechaRegistro,
            CarreraOrigenId = solicitud.CarreraOrigenId,
            CarreraDestinoId = solicitud.CarreraDestinoId,
            CarreraOrigen = solicitud.CarreraOrigen.Nombre,
            CarreraDestino = solicitud.CarreraDestino.Nombre,
            Estado = solicitud.Estado,
            Modulo = solicitud.Modulo,
            UniversidadOrigen = solicitud.UniversidadOrigen,
            UniversidadDestino = solicitud.UniversidadDestino
        };

        foreach (var sa in solicitud.Asignaturas.OrderBy(sa => sa.Id))
        {
            var origen = sa.Asignatura;
            var equivalencia = equivalencias.FirstOrDefault(e => e.AsignaturaOrigenId == origen.Id);
            var destino = equivalencia is not null
                ? catalogoDestino.FirstOrDefault(d => d.Id == equivalencia.AsignaturaDestinoId)
                : catalogoDestino.FirstOrDefault(d =>
                    string.Equals(d.Nombre.Trim(), origen.Nombre.Trim(), StringComparison.OrdinalIgnoreCase));

            var contenidoOrigen = planes.TryGetValue(origen.Id, out var planOrigen)
                ? planOrigen
                : DescribirContenido(origen);

            // HU-13/HU-19: el contenido destino solo puede venir de la asignatura destino.
            var contenidoDestino = destino is null
                ? string.Empty
                : planes.TryGetValue(destino.Id, out var planDestino)
                    ? planDestino
                    : DescribirContenido(destino);

            dto.Asignaturas.Add(new AsignaturaSolicitudInternaDto
            {
                SolicitudAsignaturaId = sa.Id,
                AsignaturaOrigenId = origen.Id,
                AsignaturaOrigen = origen.Nombre,
                NivelOrigen = origen.Nivel,
                AreaOrigen = string.IsNullOrWhiteSpace(origen.Area)
                    ? Services.AreaAsignatura.Detectar(origen.Nombre)
                    : origen.Area,
                CreditosOrigen = origen.Creditos,
                HorasOrigen = origen.Horas,
                ContenidoOrigen = contenidoOrigen,
                AsignaturaDestinoId = destino?.Id,
                AsignaturaDestino = destino?.Nombre ?? SinEquivalenteEnMallaDestino,
                NivelDestino = destino?.Nivel ?? string.Empty,
                AreaDestino = destino is null
                    ? string.Empty
                    : string.IsNullOrWhiteSpace(destino.Area)
                        ? Services.AreaAsignatura.Detectar(destino.Nombre)
                        : destino.Area,
                CreditosDestino = destino?.Creditos ?? 0,
                HorasDestino = destino?.Horas ?? 0,
                ContenidoDestino = contenidoDestino,
                EquivalenciaAplicada = equivalencia is not null
                    ? $"Equivalencia registrada: {origen.Nombre} → {destino?.Nombre ?? SinEquivalenteEnMallaDestino}"
                    : null
            });
        }

        dto.TotalAsignaturas = dto.Asignaturas.Count;
        return Ok(dto);
    }

    /// <summary>
    /// Actualiza el nombre y la cédula reales del titular del trámite. La fuente es el récord
    /// académico leído por el agente; el Coordinador también puede corregir estos datos antes
    /// de emitir el informe final. No cambia el estado de la solicitud.
    /// </summary>
    [HttpPost("{id:int}/titular")]
    public async Task<IActionResult> ActualizarTitular(
        int id, [FromBody] ActualizarTitularSolicitudRequest? request)
    {
        if (id <= 0)
            return BadRequest(new { error = "El id de la solicitud debe ser mayor que cero." });

        var solicitud = await _db.Solicitudes.FirstOrDefaultAsync(s => s.Id == id);
        if (solicitud is null)
            return NotFound(new { error = "Solicitud no encontrada." });

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
                UsuarioCorreo = string.IsNullOrWhiteSpace(request?.Usuario)
                    ? "sistema@homologauta"
                    : request.Usuario.Trim(),
                FechaCambio = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        return Ok(new
        {
            id,
            nombre = solicitud.EstudianteNombre,
            cedula = solicitud.EstudianteCedula,
            actualizado = cambio
        });
    }

    /// <summary>
    /// Cambia el estado de una solicitud y deja la entrada en el historial. Es idempotente:
    /// si el estado es el mismo, no duplica el historial.
    /// </summary>
    [HttpPost("{id:int}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] CambiarEstadoSolicitudRequest request)
    {
        if (id <= 0)
            return BadRequest(new { error = "El id de la solicitud debe ser mayor que cero." });

        if (request is null || string.IsNullOrWhiteSpace(request.Estado))
            return BadRequest(new { error = "El estado es obligatorio." });

        var estado = request.Estado.Trim().ToUpperInvariant();
        var permitidos = new[]
        {
            EstadoSolicitud.REGISTRADA,
            EstadoSolicitud.PENDIENTE_REVISION,
            EstadoSolicitud.INFORME_EMITIDO,
            EstadoSolicitud.APROBADA,
            EstadoSolicitud.RECHAZADA,
            EstadoSolicitud.RESUELTA
        };

        if (!permitidos.Contains(estado))
            return BadRequest(new { error = $"Estado no reconocido: {request.Estado}." });

        var solicitud = await _db.Solicitudes.FirstOrDefaultAsync(s => s.Id == id);
        if (solicitud is null)
            return NotFound(new { error = "Solicitud no encontrada." });

        var anterior = solicitud.Estado;
        if (string.Equals(anterior, estado, StringComparison.OrdinalIgnoreCase))
            return Ok(new { id, estado, cambiado = false });

        solicitud.Estado = estado;
        _db.HistorialEstados.Add(new HistorialEstado
        {
            SolicitudId = id,
            EstadoAnterior = anterior,
            EstadoNuevo = estado,
            UsuarioCorreo = string.IsNullOrWhiteSpace(request.Usuario)
                ? "sistema@homologauta"
                : request.Usuario.Trim(),
            FechaCambio = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return Ok(new { id, estado, cambiado = true });
    }

    /// <summary>
    /// Deja constancia en el historial de una corrección sin cambiar el estado (HU-20).
    /// </summary>
    [HttpPost("{id:int}/historial")]
    public async Task<IActionResult> RegistrarHistorial(int id, [FromBody] CambiarEstadoSolicitudRequest request)
    {
        var solicitud = await _db.Solicitudes.FirstOrDefaultAsync(s => s.Id == id);
        if (solicitud is null)
            return NotFound(new { error = "Solicitud no encontrada." });

        _db.HistorialEstados.Add(new HistorialEstado
        {
            SolicitudId = id,
            EstadoAnterior = solicitud.Estado,
            EstadoNuevo = string.IsNullOrWhiteSpace(request?.Estado) ? solicitud.Estado : request.Estado,
            UsuarioCorreo = string.IsNullOrWhiteSpace(request?.Usuario)
                ? "sistema@homologauta"
                : request.Usuario.Trim(),
            FechaCambio = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return Ok(new { id, registrado = true });
    }

    /// <summary>
    /// El catálogo no guarda sílabos, así que se describe la asignatura con sus datos reales
    /// (nombre, nivel, créditos y horas). El agente recibe ese contenido para comparar.
    /// </summary>
    private static string DescribirContenido(Asignatura asignatura) =>
        $"{asignatura.Nombre}. Nivel {asignatura.Nivel}. {asignatura.Creditos} créditos, {asignatura.Horas} horas.";
}

public class SolicitudInternaDto
{
    public int Id { get; set; }
    public string NumeroProceso { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string EstudianteCorreo { get; set; } = string.Empty;
    public string EstudianteNombre { get; set; } = string.Empty;
    public string EstudianteCedula { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
    public int CarreraOrigenId { get; set; }
    public int CarreraDestinoId { get; set; }
    public string CarreraOrigen { get; set; } = string.Empty;
    public string CarreraDestino { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string Modulo { get; set; } = "INTERNO";
    public string? UniversidadOrigen { get; set; }
    public string? UniversidadDestino { get; set; }
    public int TotalAsignaturas { get; set; }
}

public class SolicitudCompletaInternaDto : SolicitudInternaDto
{
    public List<AsignaturaSolicitudInternaDto> Asignaturas { get; set; } = new();
}

public class AsignaturaSolicitudInternaDto
{
    public int SolicitudAsignaturaId { get; set; }

    public int AsignaturaOrigenId { get; set; }
    public string AsignaturaOrigen { get; set; } = string.Empty;
    public string NivelOrigen { get; set; } = string.Empty;
    public string AreaOrigen { get; set; } = string.Empty;
    public int CreditosOrigen { get; set; }
    public int HorasOrigen { get; set; }
    public string ContenidoOrigen { get; set; } = string.Empty;

    public int? AsignaturaDestinoId { get; set; }
    public string AsignaturaDestino { get; set; } = string.Empty;
    public string NivelDestino { get; set; } = string.Empty;
    public string AreaDestino { get; set; } = string.Empty;
    public int CreditosDestino { get; set; }
    public int HorasDestino { get; set; }
    public string ContenidoDestino { get; set; } = string.Empty;

    /// <summary>Regla de equivalencia aplicada, si existe (HU-14).</summary>
    public string? EquivalenciaAplicada { get; set; }
}

public class CambiarEstadoSolicitudRequest
{
    public string Estado { get; set; } = string.Empty;
    public string? Usuario { get; set; }
}

/// <summary>Datos reales del titular del trámite, extraídos del récord o corregidos.</summary>
public class ActualizarTitularSolicitudRequest
{
    public string? Nombre { get; set; }
    public string? Cedula { get; set; }
    public string? Usuario { get; set; }
}
