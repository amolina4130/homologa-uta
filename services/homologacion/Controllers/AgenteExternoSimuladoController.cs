using HomologaUTA.Homologacion.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace HomologaUTA.Homologacion.Controllers;

/// <summary>
/// Agente externo SIMULADO, solo para pruebas locales (contrato HU-09).
/// Está apagado salvo que se active AgenteExterno:SimuladorActivo = true.
/// </summary>
[ApiController]
[Route("api/external-agent")]
public class AgenteExternoSimuladoController : ControllerBase
{
    private readonly bool _activo;

    public AgenteExternoSimuladoController(IConfiguration config)
    {
        _activo = config.GetValue<bool>("AgenteExterno:SimuladorActivo");
    }

    [HttpPost("analyze")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> Analyze([FromBody] AgenteExternoRequest request, CancellationToken ct)
    {
        if (!_activo)
            return NotFound();

        // Correo "lento...": tarda más que el tiempo de espera para probar el timeout
        if (request.Estudiante.StartsWith("lento", StringComparison.OrdinalIgnoreCase))
            await Task.Delay(TimeSpan.FromSeconds(15), ct);

        // Correo "caido...": el agente falla
        if (request.Estudiante.StartsWith("caido", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status500InternalServerError);

        var asignaturas = request.Documentos.Silabos
            .Select((s, i) => new AsignaturaAgenteExternoDto
            {
                NombreOrigen = s.Asignatura,
                NombreDestino = s.Asignatura,
                // Ambas HOMOLOGABLE, la segunda con 60 %: el sistema no le impone el umbral del 80 %
                Decision = "HOMOLOGABLE",
                Justificacion = i % 2 == 0
                    ? "Los contenidos, la profundidad y la carga horaria son equivalentes."
                    : "Aunque la coincidencia es parcial, el agente considera suficiente la formación práctica.",
                PorcentajeSimilitud = i % 2 == 0 ? 90 : 60
            })
            .ToList();

        return Ok(new AgenteExternoResponse
        {
            SolicitudId = request.SolicitudId,
            AnalisisCompletado = true,
            Asignaturas = asignaturas,
            ObservacionesGenerales = "Respuesta simulada."
        });
    }
}