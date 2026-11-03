using System.Text;
using System.Text.Json;
using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomologaUTA.Homologacion.Controllers;

/// <summary>
/// Endpoints del Coordinador (HU-10). El acceso solo para el rol COORDINADOR lo aplica el Gateway
/// con la política "coordinador" en la ruta /homologacion/coordinador.
/// </summary>
[ApiController]
[Route("homologacion/coordinador")]
public class CoordinadorController : ControllerBase
{
    private readonly CoordinadorService _servicio;

    public CoordinadorController(CoordinadorService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>Lista las solicitudes en PENDIENTE_REVISION (fecha, estudiante, tipo y carreras)</summary>
    /// <param name="ct">Cancelación de la petición</param>
    /// <response code="200">Solicitudes pendientes, las más antiguas primero</response>
    /// <response code="502">No se pudo consultar el servicio de Solicitudes</response>
    [HttpGet("pendientes")]
    [ProducesResponseType(typeof(List<PendienteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<List<PendienteDto>>> Pendientes(CancellationToken ct)
    {
        try
        {
            return Ok(await _servicio.ListarPendientesAsync(ct));
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }

    /// <summary>Detalle de la solicitud con la propuesta del agente por asignatura</summary>
    /// <param name="id">Id de la solicitud</param>
    /// <param name="ct">Cancelación de la petición</param>
    /// <response code="200">Detalle con la propuesta del agente</response>
    /// <response code="404">La solicitud no tiene análisis</response>
    /// <response code="502">No se pudo consultar el servicio de Solicitudes</response>
    [HttpGet("{id:int}/detalle")]
    [ProducesResponseType(typeof(DetalleSolicitudDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
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

    /// <summary>Emite el informe técnico con la decisión y la observación por asignatura</summary>
    /// <remarks>
    /// Debe incluir todas las asignaturas del análisis (use los ids que devuelve el detalle).
    /// La decisión es HOMOLOGABLE o NO_HOMOLOGABLE y la observación es obligatoria.
    /// Al guardarse, la solicitud pasa a INFORME_EMITIDO y sale de la lista de pendientes.
    /// </remarks>
    /// <param name="id">Id de la solicitud</param>
    /// <param name="request">Decisión y observación por asignatura</param>
    /// <param name="ct">Cancelación de la petición</param>
    /// <response code="200">Informe guardado</response>
    /// <response code="400">Informe incompleto o con datos inválidos</response>
    /// <response code="401">No se pudo identificar al Coordinador en el token</response>
    /// <response code="404">La solicitud no tiene análisis</response>
    /// <response code="409">El informe ya fue emitido</response>
    [HttpPost("{id:int}/informe")]
    [ProducesResponseType(typeof(ResultadoInformeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultadoInformeDto>> EmitirInforme(
        int id, [FromBody] EmitirInformeRequest request, CancellationToken ct)
    {
        if (id <= 0)
            return BadRequest(new { error = "El id de la solicitud debe ser mayor que cero." });

        var correo = ObtenerCorreoDelToken();
        if (correo is null)
            return Unauthorized(new { error = "No se pudo identificar al Coordinador." });

        try
        {
            return Ok(await _servicio.EmitirInformeAsync(id, request, correo, ct));
        }
        catch (InformeException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Lee el correo del payload del token. La firma y el rol ya los validó el Gateway,
    /// por eso aquí solo se lee el dato (el token emite el correo en el claim "email").
    /// </summary>
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