using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using HomologaUTA.Homologacion.Clients;
using Polly;
using Polly.Retry;

namespace HomologaUTA.Homologacion.Agents;

/// <summary>Una materia tal como la devuelve el agente a partir del récord académico.</summary>
public class MateriaRecordAgente
{
    [JsonRequired]
    [Required]
    public string Nombre { get; set; } = string.Empty;

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? Nota { get; set; }

    public string EstadoSegunDocumento { get; set; } = string.Empty;
    public string? Periodo { get; set; }
}

public class ResultadoRecordAgente
{
    public List<MateriaRecordAgente> Materias { get; set; } = new();

    /// <summary>Nombre del titular tal como aparece en el récord académico.</summary>
    public string? Nombre { get; set; }

    /// <summary>Cédula del titular tal como aparece en el récord académico.</summary>
    public string? Cedula { get; set; }
}

/// <summary>
/// Pide al agente que extraiga del texto del récord las materias aprobadas y sus notas (HU-20).
/// El JSON se valida y se reintenta con Polly, igual que el agente interno de la HU-08.
/// </summary>
public class AgenteExtractorRecord
{
    private readonly OllamaClient _ollama;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<AgenteExtractorRecord> _logger;

    public AgenteExtractorRecord(OllamaClient ollama, ILogger<AgenteExtractorRecord> logger)
    {
        _ollama = ollama;
        _logger = logger;

        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                ShouldHandle = new PredicateBuilder()
                    .Handle<JsonException>()
                    .Handle<InvalidOperationException>()
                    .Handle<TaskCanceledException>()
                    .Handle<HttpRequestException>(),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Reintento {Attempt} al extraer el récord: {Error}",
                        args.AttemptNumber,
                        args.Outcome.Exception?.Message);
                    return default;
                }
            })
            .Build();
    }

    /// <summary>
    /// Devuelve las materias extraídas o lanza <see cref="InvalidOperationException"/> si el
    /// agente no entregó un JSON válido tras los reintentos.
    /// </summary>
    public async Task<ResultadoRecordAgente> ExtraerAsync(string textoRecord, CancellationToken ct = default)
    {
        var systemPrompt =
            "Eres un experto en récords académicos de universidades ecuatorianas. " +
            "Debes devolver ÚNICAMENTE un JSON válido con la estructura pedida.";

        var userPrompt = $@"
Extrae del siguiente récord académico el nombre y la cédula del titular y TODAS las materias con
su nota, su estado según el documento (aprobado, reprobado, retirado, etc.) y el periodo si aparece.
El nombre y la cédula son datos del titular del trámite, NO un usuario ni un rol del sistema.
Devuelve un JSON con esta forma exacta:
{{
  ""nombre"": ""(nombre completo del titular o null si no aparece)"",
  ""cedula"": ""(cédula del titular o null si no aparece)"",
  ""materias"": [
    {{ ""nombre"": ""(nombre de la materia)"", ""nota"": (número o null), ""estadoSegunDocumento"": ""aprobado"", ""periodo"": ""(periodo o null)"" }}
  ]
}}
No agregues texto fuera del JSON. Si no hay materias, devuelve {{ ""nombre"": null, ""cedula"": null, ""materias"": [] }}.

RÉCORD:
{textoRecord}
";

        var respuesta = await _pipeline.ExecuteAsync(async token =>
        {
            var raw = await _ollama.GenerarRespuestaAsync($"{systemPrompt}\n\n{userPrompt}");

            var json = raw.Replace("```json", "").Replace("```", "").Trim();
            var inicio = json.IndexOf('{');
            var fin = json.LastIndexOf('}');
            if (inicio >= 0 && fin > inicio)
                json = json.Substring(inicio, fin - inicio + 1);

            var opciones = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };

            var resultado = JsonSerializer.Deserialize<ResultadoRecordAgente>(json, opciones)
                            ?? throw new JsonException("El JSON del récord está vacío.");

            if (resultado.Materias is null)
                throw new JsonException("El JSON no trae la lista de materias.");

            foreach (var materia in resultado.Materias)
            {
                if (string.IsNullOrWhiteSpace(materia.Nombre))
                    throw new JsonException("Hay una materia sin nombre.");

                materia.Nombre = materia.Nombre.Trim();

                if (materia.Nota is < 0 or > 20)
                    throw new JsonException($"La nota de {materia.Nombre} está fuera del rango 0-20.");

                materia.EstadoSegunDocumento = string.IsNullOrWhiteSpace(materia.EstadoSegunDocumento)
                    ? "aprobado"
                    : materia.EstadoSegunDocumento.Trim();
            }

            resultado.Nombre = string.IsNullOrWhiteSpace(resultado.Nombre)
                ? null
                : resultado.Nombre.Trim();
            resultado.Cedula = string.IsNullOrWhiteSpace(resultado.Cedula)
                ? null
                : resultado.Cedula.Trim();

            return resultado;
        }, ct);

        _logger.LogInformation(
            "El agente extrajo {Total} materias y los datos del titular del récord",
            respuesta.Materias.Count);
        return respuesta;
    }
}
