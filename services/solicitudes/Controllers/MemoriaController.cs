using HomologaUTA.Solicitudes.DTOs;
using HomologaUTA.Solicitudes.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomologaUTA.Solicitudes.Controllers;

[ApiController]
[Route("memoria")]
public class MemoriaController : ControllerBase
{
    private readonly MemoriaService _memoria;

    public MemoriaController(MemoriaService memoria)
    {
        _memoria = memoria;
    }

    /// <summary>Carga o actualiza la memoria del agente (HU-19, solo Administrador)</summary>
    /// <remarks>
    /// Calcula la huella de los datos de origen. Si no cambió desde la última carga responde
    /// <c>sinCambios: true</c> y no reescribe nada.
    /// </remarks>
    /// <response code="200">Resultado de la carga</response>
    /// <response code="403">El usuario no es Administrador</response>
    [HttpPost("carga")]
    [ProducesResponseType(typeof(MemoriaCargaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Cargar(CancellationToken ct)
    {
        if (EsRolDistintoDeAdministrador())
            return StatusCode(StatusCodes.Status403Forbidden, new { mensaje = "Solo el Administrador puede cargar la memoria del agente." });

        var resultado = await _memoria.CargarAsync(ct);
        return Ok(resultado);
    }

    /// <summary>Estado de la memoria del agente (HU-19)</summary>
    /// <response code="200">Estado de la memoria</response>
    /// <response code="404">Todavía no se ha cargado la memoria</response>
    [HttpGet("estado")]
    [ProducesResponseType(typeof(MemoriaEstadoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Estado(CancellationToken ct)
    {
        var estado = await _memoria.ObtenerEstadoAsync(ct);
        return estado is null
            ? NotFound(new { mensaje = "La memoria del agente aún no se ha cargado." })
            : Ok(estado);
    }

    /// <summary>Asignaturas de una carrera en la memoria, filtradas por nivel máximo (HU-19)</summary>
    /// <param name="carreraId">Id de la carrera</param>
    /// <param name="nivelMaximo">Nivel numérico máximo a devolver (opcional)</param>
    [HttpGet("carreras/{carreraId:int}/asignaturas")]
    [ProducesResponseType(typeof(List<MemoriaAsignaturaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Asignaturas(int carreraId, [FromQuery] int? nivelMaximo, CancellationToken ct)
    {
        var asignaturas = await _memoria.ObtenerAsignaturasAsync(carreraId, nivelMaximo, ct);
        return Ok(asignaturas);
    }

    private bool EsRolDistintoDeAdministrador() =>
        User.FindFirst("rol") is { } rol && rol.Value != "ADMINISTRADOR";
}
