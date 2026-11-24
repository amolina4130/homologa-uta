using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomologaUTA.Homologacion.Controllers;

/// <summary>Descarga los informes PDF de una solicitud (HU-18 y nuevo flujo de delegación).</summary>
[ApiController]
[Route("homologacion")]
public class InformeController : ControllerBase
{
    private readonly IInformeService _informe;
    private readonly SolicitudesClient _solicitudes;
    private readonly DocenteService _docente;
    private readonly CoordinadorService _coordinador;

    public InformeController(
        IInformeService informe,
        SolicitudesClient solicitudes,
        DocenteService docente,
        CoordinadorService coordinador)
    {
        _informe = informe;
        _solicitudes = solicitudes;
        _docente = docente;
        _coordinador = coordinador;
    }

    /// <summary>Devuelve el informe técnico de homologación en PDF (endpoint legacy).</summary>
    /// <remarks>El estudiante solo descarga el de sus propias solicitudes; el Coordinador, cualquiera.</remarks>
    /// <response code="200">PDF del informe</response>
    /// <response code="403">La solicitud no pertenece al estudiante</response>
    /// <response code="404">La solicitud no existe o no tiene análisis</response>
    [HttpGet("{id:int}/informe")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Descargar(int id, CancellationToken ct)
    {
        if (id <= 0)
            return BadRequest(new { mensaje = "El id de la solicitud debe ser mayor que cero." });

        try
        {
            var info = await _solicitudes.ObtenerCompletaAsync(id, ct);
            if (info is null)
                return NotFound(new { mensaje = "Solicitud no encontrada." });

            if (!AutorizacionHomologacion.PuedeAccederSolicitud(User, info.EstudianteCorreo))
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { mensaje = "No puede descargar el informe de otro estudiante." });

            var pdf = await _informe.GenerarPdfAsync(id, ct);
            var nombre = $"Informe_SOL-{id:D4}.pdf";
            return File(pdf, "application/pdf", nombre);
        }
        catch (InformeException ex)
        {
            return StatusCode(ex.StatusCode, new { mensaje = ex.Message });
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { mensaje = ex.Message });
        }
    }

    /// <summary>PDF del informe del docente asignado (query correo para llamadas internas).</summary>
    [HttpGet("{id:int}/informe-docente")]
    [Produces("application/pdf")]
    public async Task<IActionResult> DescargarInformeDocente(
        int id, [FromQuery] string? correo, CancellationToken ct)
    {
        if (id <= 0)
            return BadRequest(new { mensaje = "El id de la solicitud debe ser mayor que cero." });

        var docente = correo
                      ?? AutorizacionHomologacion.Correo(User)
                      ?? (AutorizacionHomologacion.HayIdentidad(User) ? null : "docente@homologauta");

        try
        {
            var pdf = await _docente.GenerarPdfAsync(id, docente, ct);
            return File(pdf, "application/pdf", $"Informe_Docente_SOL-{id:D4}.pdf");
        }
        catch (InformeException ex)
        {
            return StatusCode(ex.StatusCode, new { mensaje = ex.Message });
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { mensaje = ex.Message });
        }
    }

    /// <summary>PDF del informe final del Coordinador, dirigido al Consejo Directivo.</summary>
    [HttpGet("{id:int}/informe-final")]
    [Produces("application/pdf")]
    public async Task<IActionResult> DescargarInformeFinal(int id, CancellationToken ct)
    {
        if (id <= 0)
            return BadRequest(new { mensaje = "El id de la solicitud debe ser mayor que cero." });

        try
        {
            var pdf = await _coordinador.GenerarInformeFinalPdfAsync(id, ct);
            return File(pdf, "application/pdf", $"Informe_Final_SOL-{id:D4}.pdf");
        }
        catch (InformeException ex)
        {
            return StatusCode(ex.StatusCode, new { mensaje = ex.Message });
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { mensaje = ex.Message });
        }
    }
}
