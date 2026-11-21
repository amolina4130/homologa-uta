using System.Text.Json;
using System.Text.Json.Serialization;
using HomologaUTA.Homologacion.Clients;
using Polly;
using Polly.Retry;
using Microsoft.Extensions.Logging;

namespace HomologaUTA.Homologacion.Agents;

public class MateriaRecordDto
{
    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("nota")]
    public decimal? Nota { get; set; }

    [JsonPropertyName("estadoSegunDocumento")]
    public string EstadoSegunDocumento { get; set; } = string.Empty;

    [JsonPropertyName("periodo")]
    public string? Periodo { get; set; }
}

public class AgenteExtractorRecord
{
    private readonly OllamaClient _ollamaClient;
    private readonly ILogger<AgenteExtractorRecord> _logger;
    private readonly ResiliencePipeline _retryPipeline;

    public AgenteExtractorRecord(OllamaClient ollamaClient, ILogger<AgenteExtractorRecord> logger)
    {
        _ollamaClient = ollamaClient;
        _logger = logger;
        
        _retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                ShouldHandle = new PredicateBuilder()
                    .Handle<JsonException>()
                    .Handle<TaskCanceledException>()
                    .Handle<HttpRequestException>()
            })
            .Build();
    }

    public async Task<(List<MateriaRecordDto> materias, bool requiereRevision, string motivo)> ExtraerMateriasAsync(string textoRecord)
    {
        var systemPrompt = "Eres un experto en transcripción de récords académicos. Devuelve ÚNICAMENTE un JSON válido con la lista de materias.";
        var userPrompt = $@"
        Extrae las materias aprobadas del siguiente texto. Devuelve un JSON con esta estructura:
        {{
          ""materias"": [
            {{ ""nombre"": ""Nombre"", ""nota"": 4.5, ""estadoSegunDocumento"": ""Aprobado"", ""periodo"": ""2023-1"" }}
          ]
        }}
        Si no puedes leer el documento, devuelve una lista vacía.
        
        Texto del récord:
        {textoRecord}
        ";

        try
        {
            return await _retryPipeline.ExecuteAsync(async token =>
            {
                var prompt = $"{systemPrompt}\n\n{userPrompt}";
                var respuesta = await _ollamaClient.GenerarRespuestaAsync(prompt);
                var jsonLimpio = respuesta.Replace("```json", "").Replace("```", "").Trim();
                var inicio = jsonLimpio.IndexOf('{');
                var fin = jsonLimpio.LastIndexOf('}');
                if (inicio >= 0 && fin > inicio) jsonLimpio = jsonLimpio.Substring(inicio, fin - inicio + 1);

                var opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var resultado = JsonSerializer.Deserialize<ExtractorResponse>(jsonLimpio, opciones);

                if (resultado == null || resultado.Materias == null)
                    throw new JsonException("El JSON devuelto no tiene la estructura esperada.");

                return (resultado.Materias, false, string.Empty);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al extraer materias del récord.");
            return (new List<MateriaRecordDto>(), true, "No se pudo leer el documento o el agente falló.");
        }
    }

    private class ExtractorResponse
    {
        [JsonPropertyName("materias")]
        public List<MateriaRecordDto> Materias { get; set; } = new();
    }
}