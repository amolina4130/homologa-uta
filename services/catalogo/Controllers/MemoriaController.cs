using HomologaUTA.Catalogo.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomologaUTA.Catalogo.Controllers;

/// <summary>
/// Memoria del agente: carreras, niveles, asignaturas (créditos, horas, área) y contenidos mínimos.
/// El rol lo valida el API Gateway (POST /memoria/carga solo para ADMINISTRADOR).
/// </summary>
[ApiController]
[Route("memoria")]
[Produces("application/json")]
public class MemoriaController : ControllerBase
{
    private readonly MemoriaService _servicio;

    public MemoriaController(MemoriaService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>
    /// Carga la memoria desde las mallas y los planes analíticos (solo Administrador).
    /// Calcula la huella de los datos de origen: si no cambió, no modifica nada y responde
    /// sinCambios = true; si cambió, actualiza solo lo que difiere.
    /// </summary>
    /// <response code="200">Resultado de la carga: cargadas, sinCambios y huella.</response>
    /// <response code="403">El usuario no es Administrador (lo responde el gateway).</response>
    /// <response code="422">Los datos de origen no se pueden cargar.</response>
    [HttpPost("carga")]
    [ProducesResponseType(typeof(ResultadoCarga), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cargar(CancellationToken ct)
    {
        try
        {
            return Ok(await _servicio.CargarAsync(ct));
        }
        catch (MemoriaCargaException ex)
        {
            return UnprocessableEntity(new { mensaje = ex.Message });
        }
    }

    /// <summary>Huella y fecha de la última carga, y totales de la memoria.</summary>
    /// <response code="200">Estado de la memoria.</response>
    /// <response code="404">Todavía no hay memoria cargada.</response>
    [HttpGet("estado")]
    [ProducesResponseType(typeof(EstadoMemoria), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Estado(CancellationToken ct)
    {
        var estado = await _servicio.ObtenerEstadoAsync(ct);
        if (estado is null)
            return NotFound(new { mensaje = "Todavía no hay memoria cargada." });

        return Ok(estado);
    }

    /// <summary>Asignaturas de una carrera, con su área y contenidos mínimos.</summary>
    /// <param name="carreraId">Id de la carrera (el mismo de las mallas).</param>
    /// <param name="nivelMaximo">Si se indica, solo devuelve asignaturas de ese nivel hacia abajo (1 = Primero).</param>
    /// <response code="200">Lista de asignaturas ordenada por nivel y nombre.</response>
    /// <response code="400">nivelMaximo inválido.</response>
    /// <response code="404">La carrera no está en la memoria.</response>
    [HttpGet("carreras/{carreraId:int}/asignaturas")]
    [ProducesResponseType(typeof(List<AsignaturaMemoriaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Asignaturas(int carreraId, [FromQuery] int? nivelMaximo, CancellationToken ct)
    {
        if (nivelMaximo is < 1)
            return BadRequest(new { mensaje = "nivelMaximo debe ser un número mayor o igual a 1." });

        var asignaturas = await _servicio.ObtenerAsignaturasAsync(carreraId, nivelMaximo, ct);
        if (asignaturas is null)
            return NotFound(new { mensaje = "La carrera no existe en la memoria. Ejecute primero la carga." });

        return Ok(asignaturas);
    }
}
