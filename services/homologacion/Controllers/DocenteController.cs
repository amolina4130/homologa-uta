using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomologaUTA.Homologacion.Controllers;

/// <summary>Endpoints del docente asignado por el Coordinador.</summary>
[ApiController]
[Route("homologacion/docente")]
public class DocenteController : ControllerBase
{
    private readonly DocenteService _servicio;

    public DocenteController(DocenteService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>Solicitudes delegadas al docente autenticado.</summary>
    [HttpGet("solicitudes")]
    public async Task<ActionResult<List<SolicitudDocenteDto>>> Solicitudes(CancellationToken ct)
    {
        if (!AutorizacionHomologacion.EsRol(User, "DOCENTE", "ADMINISTRADOR"))
            return Forbid();

        try
        {
            return Ok(await _servicio.ListarSolicitudesAsync(CorreoDocente(), ct));
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }

    /// <summary>Detalle real de una solicitud asignada y su informe sugerido.</summary>
    [HttpGet("{id:int}/detalle")]
    public async Task<ActionResult<DetalleDocenteDto>> Detalle(int id, CancellationToken ct)
    {
        if (id <= 0)
            return BadRequest(new { error = "El id de la solicitud debe ser mayor que cero." });

        if (!AutorizacionHomologacion.EsRol(User, "DOCENTE", "ADMINISTRADOR"))
            return Forbid();

        try
        {
            return Ok(await _servicio.ObtenerDetalleAsync(id, CorreoDocente(), ct));
        }
        catch (InformeException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }

    /// <summary>
    /// PASO 1: genera la sugerencia con IA y la guarda como borrador. No envía nada al
    /// Coordinador; el Docente debe revisarla y enviarla explícitamente después.
    /// </summary>
    [HttpPost("{id:int}/sugerencia")]
    public async Task<ActionResult<SugerenciaDocenteDto>> GenerarSugerencia(
        int id, [FromBody] GenerarSugerenciaRequest request, CancellationToken ct)
    {
        if (id <= 0)
            return BadRequest(new { error = "El id de la solicitud debe ser mayor que cero." });

        if (!AutorizacionHomologacion.EsRol(User, "DOCENTE", "ADMINISTRADOR"))
            return Forbid();

        try
        {
            return Ok(await _servicio.GenerarSugerenciaAsync(id, CorreoDocente(), request, ct));
        }
        catch (InformeException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }

    /// <summary>
    /// "IA me recomienda qué materias homologar": solo solicitudes INTERNO, con catálogo,
    /// planes analíticos, créditos y horas ya cargados en la memoria del agente.
    /// </summary>
    [HttpGet("{id:int}/recomendacion")]
    public async Task<ActionResult<RecomendacionIAInternaDto>> Recomendacion(int id, CancellationToken ct)
    {
        if (id <= 0)
            return BadRequest(new { error = "El id de la solicitud debe ser mayor que cero." });

        if (!AutorizacionHomologacion.EsRol(User, "DOCENTE", "ADMINISTRADOR"))
            return Forbid();

        try
        {
            return Ok(await _servicio.ObtenerRecomendacionInternaAsync(id, CorreoDocente(), ct));
        }
        catch (InformeException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }

    /// <summary>PASO 2: el Docente envía manualmente su informe sugerido al Coordinador.</summary>
    [HttpPost("{id:int}/evaluar")]
    public async Task<ActionResult<ResultadoEvaluacionDto>> Evaluar(
        int id, [FromBody] EvaluarDocenteRequest request, CancellationToken ct)
    {
        if (id <= 0)
            return BadRequest(new { error = "El id de la solicitud debe ser mayor que cero." });

        if (!AutorizacionHomologacion.EsRol(User, "DOCENTE", "ADMINISTRADOR"))
            return Forbid();

        try
        {
            return Ok(await _servicio.EvaluarAsync(id, CorreoDocente(), request, ct));
        }
        catch (InformeException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }

    /// <summary>El Docente genera (o repite) con la IA el análisis de las materias aprobadas.</summary>
    [HttpPost("{id:int}/analizar")]
    public async Task<ActionResult<DetalleDocenteDto>> Analizar(int id, CancellationToken ct)
    {
        if (id <= 0)
            return BadRequest(new { error = "El id de la solicitud debe ser mayor que cero." });

        if (!AutorizacionHomologacion.EsRol(User, "DOCENTE", "ADMINISTRADOR"))
            return Forbid();

        try
        {
            return Ok(await _servicio.AnalizarAsync(id, CorreoDocente(), ct));
        }
        catch (InformeException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }

    /// <summary>El Docente pide documentación adicional al Coordinador antes de decidir.</summary>
    [HttpPost("{id:int}/solicitar-documentacion")]
    public async Task<ActionResult<DelegacionDto>> SolicitarDocumentacion(
        int id, [FromBody] SolicitarDocumentacionRequest request, CancellationToken ct)
    {
        if (id <= 0)
            return BadRequest(new { error = "El id de la solicitud debe ser mayor que cero." });

        if (!AutorizacionHomologacion.EsRol(User, "DOCENTE", "ADMINISTRADOR"))
            return Forbid();

        try
        {
            return Ok(await _servicio.SolicitarDocumentacionAsync(id, CorreoDocente(), request, ct));
        }
        catch (InformeException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    /// <summary>PDF del informe del docente asignado.</summary>
    [HttpGet("{id:int}/informe")]
    [Produces("application/pdf")]
    public async Task<IActionResult> Informe(int id, CancellationToken ct)
    {
        if (id <= 0)
            return BadRequest(new { error = "El id de la solicitud debe ser mayor que cero." });

        if (!AutorizacionHomologacion.EsRol(User, "DOCENTE", "ADMINISTRADOR"))
            return Forbid();

        try
        {
            var pdf = await _servicio.GenerarPdfAsync(id, CorreoDocente(), ct);
            return File(pdf, "application/pdf", $"Informe_Docente_SOL-{id:D4}.pdf");
        }
        catch (InformeException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Correo del token. Sin identidad (llamada interna) se usa el correo de desarrollo,
    /// igual que hacen los demás helpers; las políticas del Gateway se aplican en la puerta.
    /// </summary>
    private string? CorreoDocente() =>
        AutorizacionHomologacion.Correo(User)
        ?? (AutorizacionHomologacion.HayIdentidad(User) ? null : "docente@homologauta");
}
