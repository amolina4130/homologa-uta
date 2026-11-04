using System.Text;
using System.Text.Json;
using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomologaUTA.Homologacion.Controllers;

[ApiController]
[Route("homologacion")]
public class CoordinadorController : ControllerBase
{
    private readonly CoordinadorService _servicio;

    public CoordinadorController(CoordinadorService servicio)
    {
        _servicio = servicio;
    }

    // Endpoint para el Coordinador (Lista de pendientes)
    [HttpGet("coordinador/pendientes")]
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

    // NUEVO Endpoint para el Estudiante (Ver su propio resultado)
    [HttpGet("estudiante/{id:int}/detalle")]
    public async Task<ActionResult<DetalleSolicitudDto>> DetalleEstudiante(int id, CancellationToken ct)
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

    // Endpoint para el Coordinador (Emitir informe)
    [HttpPost("coordinador/{id:int}/informe")]
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