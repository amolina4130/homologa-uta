using System.Security.Claims;
using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Models;
using HomologaUTA.Homologacion.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Homologacion.Controllers;

/// <summary>Extracción y corrección de las materias aprobadas del récord académico (HU-20).</summary>
[ApiController]
[Route("homologacion")]
public class RecordAcademicoController : ControllerBase
{
    private readonly ExtractorRecordService _extractor;
    private readonly PreseleccionService _preseleccion;
    private readonly SolicitudesClient _solicitudes;
    private readonly HomologacionDbContext _db;

    public RecordAcademicoController(
        ExtractorRecordService extractor,
        PreseleccionService preseleccion,
        SolicitudesClient solicitudes,
        HomologacionDbContext db)
    {
        _extractor = extractor;
        _preseleccion = preseleccion;
        _solicitudes = solicitudes;
        _db = db;
    }

    /// <summary>Extrae las materias aprobadas y sus notas del récord ya subido (HU-20)</summary>
    /// <response code="200">Materias extraídas (puede requerir revisión con el motivo)</response>
    /// <response code="404">La solicitud no existe</response>
    /// <response code="422">El récord no tiene texto legible</response>
    [HttpPost("{solicitudId:int}/record/extraer")]
    [ProducesResponseType(typeof(ExtraerRecordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Extraer(int solicitudId, CancellationToken ct)
    {
        try
        {
            return Ok(await _extractor.ExtraerAsync(solicitudId, ct));
        }
        catch (ExtraccionException ex)
        {
            return StatusCode(ex.StatusCode, new { mensaje = ex.Message });
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { mensaje = ex.Message });
        }
        catch (DocumentosNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { mensaje = ex.Message });
        }
    }

    /// <summary>Lista las materias aprobadas guardadas para la solicitud (HU-20)</summary>
    [HttpGet("{solicitudId:int}/materias-aprobadas")]
    [ProducesResponseType(typeof(List<MateriaAprobadaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerMaterias(int solicitudId, CancellationToken ct)
    {
        var detalle = await _solicitudes.ObtenerDetalleAsync(solicitudId, ct);
        if (detalle is null) return NotFound(new { mensaje = "Solicitud no encontrada." });
        if (!PuedeConsultar(detalle.EstudianteCorreo))
            return StatusCode(StatusCodes.Status403Forbidden, new { mensaje = "No puede consultar una solicitud de otro estudiante." });

        var materias = await _db.MateriasAprobadas
            .AsNoTracking()
            .Where(m => m.SolicitudId == solicitudId)
            .OrderBy(m => m.Id)
            .ToListAsync(ct);

        return Ok(materias.Select(ExtractorRecordService.ADto).ToList());
    }

    /// <summary>Reemplaza la lista de materias aprobadas corregida por el estudiante (HU-20)</summary>
    /// <response code="200">Lista guardada</response>
    /// <response code="403">La solicitud es de otro estudiante</response>
    /// <response code="409">La solicitud ya pasó a revisión</response>
    [HttpPut("{solicitudId:int}/materias-aprobadas")]
    [ProducesResponseType(typeof(List<MateriaAprobadaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GuardarMaterias(
        int solicitudId, [FromBody] List<MateriaAprobadaRequest> request, CancellationToken ct)
    {
        var detalle = await _solicitudes.ObtenerDetalleAsync(solicitudId, ct);
        if (detalle is null) return NotFound(new { mensaje = "Solicitud no encontrada." });

        var correo = Correo();
        if (!string.Equals(correo, detalle.EstudianteCorreo, StringComparison.OrdinalIgnoreCase)
            && !EsAdministrador())
            return StatusCode(StatusCodes.Status403Forbidden, new { mensaje = "No puede corregir las materias de otra solicitud." });

        if (!detalle.Editable)
            return Conflict(new { mensaje = "La solicitud ya pasó a revisión y no se pueden corregir sus materias." });

        var invalida = request.FirstOrDefault(m => string.IsNullOrWhiteSpace(m.Nombre));
        if (invalida is not null)
            return BadRequest(new { mensaje = "Todas las materias deben tener nombre." });

        var anteriores = await _db.MateriasAprobadas.Where(m => m.SolicitudId == solicitudId).ToListAsync(ct);
        _db.MateriasAprobadas.RemoveRange(anteriores);

        foreach (var item in request)
        {
            _db.MateriasAprobadas.Add(new MateriaAprobada
            {
                SolicitudId = solicitudId,
                Nombre = Recortar(item.Nombre.Trim(), 200)!,
                Nota = item.Nota,
                EstadoSegunDocumento = Recortar(item.EstadoSegunDocumento, 50),
                Periodo = Recortar(item.Periodo, 50),
                AsignaturaId = item.AsignaturaId,
                FechaExtraccion = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);

        try
        {
            await _solicitudes.RegistrarHistorialAsync(solicitudId, correo ?? detalle.EstudianteCorreo, ct);
        }
        catch (SolicitudesNoDisponibleException)
        {
            // La corrección ya quedó guardada; el historial se puede recuperar después.
        }

        var guardadas = await _db.MateriasAprobadas
            .AsNoTracking()
            .Where(m => m.SolicitudId == solicitudId)
            .OrderBy(m => m.Id)
            .ToListAsync(ct);

        return Ok(guardadas.Select(ExtractorRecordService.ADto).ToList());
    }

    /// <summary>Preselecciona materias candidatas de destino por área y nivel (HU-21)</summary>
    /// <response code="200">Pares materia de origen / candidatas de destino</response>
    /// <response code="409">Sin materias aprobadas o memoria no cargada</response>
    [HttpPost("{solicitudId:int}/preseleccion")]
    [ProducesResponseType(typeof(PreseleccionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Preseleccionar(int solicitudId, CancellationToken ct)
    {
        try
        {
            return Ok(await _preseleccion.PreseleccionarAsync(solicitudId, ct));
        }
        catch (PreseleccionException ex)
        {
            return StatusCode(ex.StatusCode, new { mensaje = ex.Message });
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { mensaje = ex.Message });
        }
        catch (CatalogoNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { mensaje = ex.Message });
        }
    }

    private bool PuedeConsultar(string propietario)
    {
        var correo = Correo();
        return string.IsNullOrEmpty(correo)
               || string.Equals(correo, propietario, StringComparison.OrdinalIgnoreCase)
               || User.FindFirstValue("rol") is "COORDINADOR" or "ADMINISTRADOR" or "SECRETARIA" or "CONSEJO";
    }

    private bool EsAdministrador() => User.FindFirstValue("rol") == "ADMINISTRADOR";

    private string? Correo() =>
        User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");

    private static string? Recortar(string? texto, int max) =>
        texto is null || texto.Length <= max ? texto : texto[..max];
}
