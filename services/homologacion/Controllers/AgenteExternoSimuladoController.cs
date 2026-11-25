using HomologaUTA.Homologacion.Agents;
using HomologaUTA.Homologacion.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace HomologaUTA.Homologacion.Controllers;

/// <summary>
/// Agente externo local (contrato HU-09, ampliado en HU-22). Se activa con
/// AgenteExterno:SimuladorActivo = true cuando no hay un agente externo remoto. No inventa:
/// compara el sílabo de la universidad de origen (documentos de la carpeta) con la asignatura
/// de destino de FISEI usando criterios generales de equivalencia (ComparadorSilabos).
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
            ObservacionesGenerales = "Comparación de contenidos con criterios generales de equivalencia académica."
        });
    }

    private const int UmbralGeneral = 80;

    /// <summary>
    /// Con la preselección (HU-21/HU-22) responde por cada asignatura de destino: compara el
    /// sílabo de cada materia aprobada candidata con el contenido de destino. Si ninguna alcanza
    /// sola el umbral y hay dos candidatas, prueba la combinación de ambas. Sin sílabo de origen
    /// no decide: pide revisión e indica el documento que falta.
    /// </summary>
    private static List<AsignaturaAgenteExternoDto> SimularDesdePreseleccion(AgenteExternoRequest request)
    {
        var lista = new List<AsignaturaAgenteExternoDto>();

        foreach (var destino in request.MateriasDestino)
        {
            var candidatas = destino.MateriasOrigenPermitidas
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => m.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var conSilabo = candidatas
                .Select(m => (Materia: m, Silabo: SilaboDe(request, m)))
                .Where(x => x.Silabo is not null)
                .Select(x => (x.Materia, Comparacion: ComparadorSilabos.Comparar(
                    x.Silabo, null, null, destino.Contenido, null, null)))
                .OrderByDescending(x => x.Comparacion.Contenido)
                .ToList();

            if (conSilabo.Count == 0)
            {
                var materia = candidatas.FirstOrDefault() ?? string.Empty;
                lista.Add(new AsignaturaAgenteExternoDto
                {
                    AsignaturaDestinoId = destino.AsignaturaId > 0 ? destino.AsignaturaId : null,
                    NombreDestino = destino.Nombre,
                    NombreOrigen = materia,
                    MateriasOrigen = string.IsNullOrWhiteSpace(materia) ? new List<string>() : new List<string> { materia },
                    Decision = "REQUIERE_REVISION",
                    Justificacion = $"No se encontró el sílabo de {materia} en la documentación de origen; " +
                                    "no es posible comparar contenidos sin ese documento.",
                    PorcentajeSimilitud = 0
                });
                continue;
            }

            var origenes = new List<string> { conSilabo[0].Materia };
            var mejor = conSilabo[0].Comparacion;
            // Solo se combina con una materia relacionada por nombre con el destino.
            if (mejor.Contenido < UmbralGeneral && conSilabo.Count >= 2 &&
                Services.PreseleccionService.SimilitudNombre(conSilabo[1].Materia, destino.Nombre) >= 0.3)
            {
                var combinada = ComparadorSilabos.Comparar(
                    $"{SilaboDe(request, conSilabo[0].Materia)}\n{SilaboDe(request, conSilabo[1].Materia)}",
                    null, null, destino.Contenido, null, null);
                if (combinada.Contenido >= UmbralGeneral)
                {
                    origenes.Add(conSilabo[1].Materia);
                    mejor = combinada;
                }
            }

            var homologable = mejor.Contenido >= UmbralGeneral;
            lista.Add(new AsignaturaAgenteExternoDto
            {
                AsignaturaDestinoId = destino.AsignaturaId > 0 ? destino.AsignaturaId : null,
                NombreDestino = destino.Nombre,
                NombreOrigen = origenes[0],
                MateriasOrigen = origenes,
                Decision = homologable ? "HOMOLOGABLE" : "NO_HOMOLOGABLE",
                Justificacion =
                    $"El sílabo de origen ({string.Join(" + ", origenes)}) cubre el {mejor.Contenido}% de los temas de " +
                    $"{destino.Nombre}. " +
                    (homologable
                        ? "Los contenidos son equivalentes según criterios generales; verifique horas y créditos en los documentos."
                        : $"No alcanza el {UmbralGeneral}% de equivalencia de contenidos."),
                PorcentajeSimilitud = mejor.Contenido
            });
        }

        return lista;
    }

    private static string? SilaboDe(AgenteExternoRequest request, string materia) =>
        request.Documentos.Silabos
            .FirstOrDefault(s => string.Equals(s.Asignatura?.Trim(), materia, StringComparison.OrdinalIgnoreCase))
            ?.Contenido is { Length: > 0 } texto ? texto : null;

    /// <summary>Sin preselección: no hay malla de destino con la cual comparar, así que se pide revisión.</summary>
    private static List<AsignaturaAgenteExternoDto> SimularDesdeSilabos(AgenteExternoRequest request) =>
        request.Documentos.Silabos
            .Select(s => new AsignaturaAgenteExternoDto
            {
                NombreOrigen = s.Asignatura,
                NombreDestino = s.Asignatura,
                MateriasOrigen = new List<string> { s.Asignatura },
                Decision = "REQUIERE_REVISION",
                Justificacion = "No se recibió la asignatura de destino de FISEI para comparar este sílabo.",
                PorcentajeSimilitud = 0
            })
            .ToList();
}
