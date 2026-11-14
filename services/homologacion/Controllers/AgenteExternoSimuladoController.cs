using HomologaUTA.Homologacion.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace HomologaUTA.Homologacion.Controllers;

/// <summary>
/// Agente externo SIMULADO, solo para pruebas locales (contrato HU-09, ampliado en HU-22).
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
            await Task.Delay(TimeSpan.FromSeconds(35), ct);

        // Correo "caido...": el agente falla
        if (request.Estudiante.StartsWith("caido", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status500InternalServerError);

        var asignaturas = request.MateriasDestino.Count > 0
            ? SimularDesdePreseleccion(request)
            : SimularDesdeSilabos(request);

        return Ok(new AgenteExternoResponse
        {
            SolicitudId = request.SolicitudId,
            AnalisisCompletado = true,
            Asignaturas = asignaturas,
            ObservacionesGenerales = "Respuesta simulada."
        });
    }

    /// <summary>
    /// Cuando la petición trae la preselección (HU-21/HU-22), responde por cada asignatura de
    /// destino. Si el destino tiene dos materias aprobadas candidatas, propone una combinación
    /// determinista (las dos primeras); si solo hay una, propone esa. Así se ejercita el caso
    /// de combinación sin depender de un agente real.
    /// </summary>
    private static List<AsignaturaAgenteExternoDto> SimularDesdePreseleccion(AgenteExternoRequest request)
    {
        var lista = new List<AsignaturaAgenteExternoDto>();

        for (var i = 0; i < request.MateriasDestino.Count; i++)
        {
            var destino = request.MateriasDestino[i];

            var origenes = destino.MateriasOrigenPermitidas
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => m.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(2)
                .ToList();

            if (origenes.Count == 0 && !string.IsNullOrWhiteSpace(request.MateriasOrigen.FirstOrDefault()))
                origenes.Add(request.MateriasOrigen.First().Trim());

            lista.Add(new AsignaturaAgenteExternoDto
            {
                AsignaturaDestinoId = destino.AsignaturaId > 0 ? destino.AsignaturaId : null,
                NombreDestino = destino.Nombre,
                NombreOrigen = origenes.FirstOrDefault() ?? string.Empty,
                MateriasOrigen = origenes,
                // Ambas HOMOLOGABLE, algunas con 60 %: el sistema no le impone el umbral del 80 %
                Decision = "HOMOLOGABLE",
                Justificacion = origenes.Count == 2
                    ? "La asignatura de destino se cubre entre las dos materias aprobadas: " +
                      "los contenidos se complementan y en conjunto alcanzan la profundidad y la carga requeridas."
                    : i % 2 == 0
                        ? "Los contenidos, la profundidad y la carga horaria son equivalentes."
                        : "Aunque la coincidencia es parcial, el agente considera suficiente la formación práctica.",
                PorcentajeSimilitud = origenes.Count == 2 ? 95 : i % 2 == 0 ? 90 : 60
            });
        }

        return lista;
    }

    /// <summary>Comportamiento anterior, cuando la petición no trae preselección.</summary>
    private static List<AsignaturaAgenteExternoDto> SimularDesdeSilabos(AgenteExternoRequest request) =>
        request.Documentos.Silabos
            .Select((s, i) => new AsignaturaAgenteExternoDto
            {
                NombreOrigen = s.Asignatura,
                NombreDestino = s.Asignatura,
                MateriasOrigen = new List<string> { s.Asignatura },
                Decision = "HOMOLOGABLE",
                Justificacion = i % 2 == 0
                    ? "Los contenidos, la profundidad y la carga horaria son equivalentes."
                    : "Aunque la coincidencia es parcial, el agente considera suficiente la formación práctica.",
                PorcentajeSimilitud = i % 2 == 0 ? 90 : 60
            })
            .ToList();
}
