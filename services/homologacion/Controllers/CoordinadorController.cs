using System.Text;
using System.Text.Json;
using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Models;
using HomologaUTA.Homologacion.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomologaUTA.Homologacion.Controllers;

[ApiController]
[Route("homologacion")]
public class CoordinadorController : ControllerBase
{
    private readonly CoordinadorService _servicio;
    private readonly DocenteService _docente;

    public CoordinadorController(CoordinadorService servicio, DocenteService docente)
    {
        _servicio = servicio;
        _docente = docente;
    }

    /// <summary>
    /// Solicitudes en revisión del Coordinador. Con <c>incluirEmitidos=true</c> también devuelve
    /// las que ya tienen informe final (trámite terminado), para consultarlas o corregirlas.
    /// </summary>
    [HttpGet("coordinador/pendientes")]
    public async Task<ActionResult<List<PendienteDto>>> Pendientes(
        [FromQuery] bool incluirEmitidos, CancellationToken ct)
    {
        try
        {
            return Ok(await _servicio.ListarPendientesAsync(incluirEmitidos, ct));
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }

    // Endpoint para el Coordinador (Ver detalle de una solicitud)
    [HttpGet("coordinador/{id:int}/detalle")]
    public async Task<ActionResult<DetalleSolicitudDto>> Detalle(int id, CancellationToken ct)
    {
        if (id <= 0)
            return BadRequest(new { error = "El id de la solicitud debe ser mayor que cero." });

        try
        {
            var detalle = await _servicio.ObtenerDetalleAsync(id, ct);
            return detalle is null
                ? NotFound(new { error = "La solicitud no tiene un análisis para revisar." })
                : Ok(detalle);
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }

    // El Comité consulta el resultado del expediente que subió.
    [HttpGet("comite/{id:int}/detalle")]
    public async Task<ActionResult<DetalleSolicitudDto>> DetalleComite(int id, CancellationToken ct)
    {
        if (!AutorizacionHomologacion.EsComiteOAdministrador(User))
            return Forbid();

        if (id <= 0)
            return BadRequest(new { error = "El id de la solicitud debe ser mayor que cero." });

        try
        {
            var detalle = await _servicio.ObtenerDetalleAsync(id, ct);
            return detalle is null
                ? NotFound(new { error = "La solicitud no tiene un análisis para revisar." })
                : Ok(detalle);
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }

    // Endpoint para el Coordinador (Emitir informe)
    [HttpPost("coordinador/{id:int}/informe")]
    public async Task<ActionResult<ResultadoInformeDto>> EmitirInforme(
        int id, [FromBody] EmitirInformeRequest request, CancellationToken ct)
    {
        if (id <= 0)
            return BadRequest(new { error = "El id de la solicitud debe ser mayor que cero." });

        var correo = CorreoCoordinador();

        try
        {
            return Ok(await _servicio.EmitirInformeAsync(id, request, correo, ct));
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

    // ------------------------------------------------------------------
    // Delegación a docentes e informe final para el Consejo Directivo
    // ------------------------------------------------------------------

    /// <summary>Delega la solicitud a uno o dos docentes (reemplaza delegaciones anteriores).</summary>
    [HttpPost("coordinador/{id:int}/delegar")]
    public async Task<ActionResult<List<DelegacionDto>>> Delegar(
        int id, [FromBody] DelegarRequest request, CancellationToken ct)
    {
        if (id <= 0)
            return BadRequest(new { error = "El id de la solicitud debe ser mayor que cero." });

        if (!AutorizacionHomologacion.EsCoordinadorOAdministrador(User))
            return Forbid();

        try
        {
            var correos = request?.Correos ?? new List<string>();
            return Ok(await _servicio.DelegarAsync(id, correos, CorreoCoordinador(), ct));
        }
        catch (InformeException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    /// <summary>Delegaciones actuales de la solicitud.</summary>
    [HttpGet("coordinador/{id:int}/delegaciones")]
    public async Task<ActionResult<List<DelegacionDto>>> Delegaciones(int id, CancellationToken ct)
    {
        if (id <= 0)
            return BadRequest(new { error = "El id de la solicitud debe ser mayor que cero." });

        if (!AutorizacionHomologacion.EsCoordinadorOAdministrador(User))
            return Forbid();

        return Ok(await _servicio.ObtenerDelegacionesAsync(id, ct));
    }

    /// <summary>Resultados conjuntos de los docentes y borrador del informe final.</summary>
    [HttpGet("coordinador/{id:int}/consolidado")]
    public async Task<ActionResult<ConsolidadoDto>> Consolidado(int id, CancellationToken ct)
    {
        if (id <= 0)
            return BadRequest(new { error = "El id de la solicitud debe ser mayor que cero." });

        if (!AutorizacionHomologacion.EsCoordinadorOAdministrador(User))
            return Forbid();

        try
        {
            return Ok(await _servicio.ObtenerConsolidadoAsync(id, ct));
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
    /// PDF del informe sugerido que el Docente envió al Coordinador. No reemplaza al informe
    /// final: el Coordinador lo usa para revisar y luego emitir su propio documento.
    /// </summary>
    [HttpGet("coordinador/{id:int}/informe-docente/pdf")]
    [Produces("application/pdf")]
    public async Task<IActionResult> InformeDocentePdf(int id, CancellationToken ct)
    {
        if (id <= 0)
            return BadRequest(new { error = "El id de la solicitud debe ser mayor que cero." });

        if (!AutorizacionHomologacion.EsCoordinadorOAdministrador(User))
            return Forbid();

        try
        {
            var consolidado = await _servicio.ObtenerConsolidadoAsync(id, ct);
            var correoDocente = consolidado.RevisadoPor
                ?? consolidado.Delegaciones.FirstOrDefault(d =>
                    d.Estado == EstadoDelegacionDocente.REVISADO &&
                    !string.IsNullOrWhiteSpace(d.InformeTexto))?.DocenteCorreo;

            if (string.IsNullOrWhiteSpace(correoDocente))
                return StatusCode(StatusCodes.Status409Conflict,
                    new { error = "El Docente todavía no ha enviado su informe sugerido." });

            var pdf = await _docente.GenerarPdfAsync(id, correoDocente, ct);
            return File(pdf, "application/pdf", $"Informe_sugerido_docente_SOL-{id:D4}.pdf");
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

    /// <summary>Emite (o regenera) el informe final dirigido al Consejo Directivo.</summary>
    [HttpPost("coordinador/{id:int}/informe-final")]
    public async Task<ActionResult<ResultadoInformeDto>> EmitirInformeFinal(
        int id, [FromBody] InformeFinalRequest request, CancellationToken ct)
    {
        if (id <= 0)
            return BadRequest(new { error = "El id de la solicitud debe ser mayor que cero." });

        if (!AutorizacionHomologacion.EsCoordinadorOAdministrador(User))
            return Forbid();

        try
        {
            var correo = CorreoCoordinador();
            var resultado = await _servicio.EmitirInformeFinalAsync(id, request, correo, ct);
            return Ok(resultado);
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

    /// <summary>Genera una vista previa editable del informe final sin emitirlo.</summary>
    [HttpPost("coordinador/{id:int}/informe-final/vista-previa")]
    public async Task<ActionResult<object>> VistaPreviaInformeFinal(
        int id, [FromBody] InformeFinalRequest request, CancellationToken ct)
    {
        if (id <= 0)
            return BadRequest(new { error = "El id de la solicitud debe ser mayor que cero." });

        if (!AutorizacionHomologacion.EsCoordinadorOAdministrador(User))
            return Forbid();

        try
        {
            var correo = CorreoCoordinador();
            var texto = await _servicio.GenerarVistaPreviaInformeFinalAsync(id, request, correo, ct);
            return Ok(new { texto });
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

    /// <summary>PDF del informe final con la plantilla del Coordinador.</summary>
    [HttpGet("coordinador/{id:int}/informe-final/pdf")]
    [Produces("application/pdf")]
    public async Task<IActionResult> InformeFinalPdf(int id, CancellationToken ct)
    {
        if (id <= 0)
            return BadRequest(new { error = "El id de la solicitud debe ser mayor que cero." });

        if (!AutorizacionHomologacion.EsCoordinadorOAdministrador(User))
            return Forbid();

        try
        {
            var pdf = await _servicio.GenerarInformeFinalPdfAsync(id, ct);
            return File(pdf, "application/pdf", $"Informe_Final_SOL-{id:D4}.pdf");
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

    private string CorreoCoordinador() =>
        AutorizacionHomologacion.Correo(User)
        ?? ObtenerCorreoDelToken()
        ?? "coordinador@homologauta";

    private string? ObtenerCorreoDelToken()
    {
        var cabecera = Request.Headers.Authorization.ToString();
        if (!cabecera.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var partes = cabecera["Bearer ".Length..].Trim().Split('.');
        if (partes.Length != 3)
            return null;

        try
        {
            var payload = partes[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            foreach (var nombre in new[] { "email", "correo" })
            {
                if (doc.RootElement.TryGetProperty(nombre, out var valor) &&
                    valor.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(valor.GetString()))
                    return valor.GetString();
            }
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }

        return null;
    }
}