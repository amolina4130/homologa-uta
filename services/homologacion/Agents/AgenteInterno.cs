using System.Text.Json;
using System.Text.Json.Serialization;
using Polly;
using Polly.Retry;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace HomologaUTA.Homologacion.Agents;

public class ResultadoAgente
{
    // 2. Marcar como requeridos y validar rangos (Criterio 2)
    [JsonRequired]
    [Range(0, 100)]
    public int Contenido { get; set; }
    
    [JsonRequired]
    [Range(0, 100)]
    public int Profundidad { get; set; }
    
    [JsonRequired]
    [Range(0, 100)]
    public int Horas { get; set; }
    
    public string Justificacion { get; set; } = string.Empty;
    public bool RequiereRevision { get; set; } = false;
}

public class AgenteInterno
{
    private readonly Clients.OllamaClient _ollamaClient;
    private readonly ResiliencePipeline _retryPipeline;
    private readonly ILogger<AgenteInterno> _logger;

    public AgenteInterno(Clients.OllamaClient ollamaClient, ILogger<AgenteInterno> logger)
    {
        _ollamaClient = ollamaClient;
        _logger = logger;
        
        // 1. Polly corregido: maneja TaskCanceledException (Timeout) y HttpRequestException (Caída)
        _retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                ShouldHandle = new PredicateBuilder()
                    .Handle<JsonException>()
                    .Handle<TaskCanceledException>() // Timeout de HttpClient
                    .Handle<HttpRequestException>(), // Ollama caído
                OnRetry = args =>
                {
                    // 3. Loggear reintentos
                    _logger.LogWarning("Reintento {Attempt} por error: {Exception}", args.AttemptNumber, args.Outcome.Exception?.Message);
                    return default;
                }
            })
            .Build();
    }

    public async Task<ResultadoAgente> AnalizarAsignaturaAsync(string asignaturaOrigen, string contenidoOrigen, string asignaturaDestino, string contenidoDestino)
    {
        // 4. Usar el campo "system" y "format" de Ollama
        var systemPrompt = "Eres un experto en homologación académica de la Universidad Técnica de Ambato. " +
                           "Debes comparar asignaturas y devolver ÚNICAMENTE un JSON válido con la estructura especificada.";
                           
        var userPrompt = $@"
        Compara la siguiente asignatura de origen con la de destino y devuelve un JSON con esta estructura exacta:
        {{
        ""contenido"": (número del 0 al 100),
        ""profundidad"": (número del 0 al 100),
        ""horas"": (número del 0 al 100),
        ""justificacion"": ""(una breve explicación)""
        }}

        Asignatura Origen: {asignaturaOrigen}
        Contenido: {contenidoOrigen}

        Asignatura Destino: {asignaturaDestino}
        Contenido: {contenidoDestino}
        ";

        try
        {
            return await _retryPipeline.ExecuteAsync(async token =>
            {
                var promptCompleto = $"{systemPrompt}\n\n{userPrompt}";
                var respuestaRaw = await _ollamaClient.GenerarRespuestaAsync(promptCompleto);
                var jsonLimpio = respuestaRaw.Replace("```json", "").Replace("```", "").Trim();
                
                var opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var resultado = JsonSerializer.Deserialize<ResultadoAgente>(jsonLimpio, opciones);

                if (resultado == null)
                    throw new JsonException("El JSON devuelto está vacío.");

                // Validar rangos manualmente para lanzar excepción y que Polly reintente
                if (resultado.Contenido < 0 || resultado.Contenido > 100 ||
                    resultado.Profundidad < 0 || resultado.Profundidad > 100 ||
                    resultado.Horas < 0 || resultado.Horas > 100)
                {
                    throw new JsonException("Los porcentajes están fuera del rango 0-100.");
                }

                return resultado;
            });
        }
        catch (Exception ex)
        {
            // 3. Loguear el error real antes de devolver el fallback
            _logger.LogError(ex, "El agente falló tras los reintentos para la asignatura {Asignatura}", asignaturaOrigen);
            
            return new ResultadoAgente
            {
                Contenido = 0,
                Profundidad = 0,
                Horas = 0,
                Justificacion = "El agente no pudo generar una respuesta válida tras varios intentos. Requiere revisión manual.",
                RequiereRevision = true
            };
        }
    }
}