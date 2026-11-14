using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Polly;
using Polly.Retry;

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

    /// <summary>
    /// Una o dos materias de origen que cubren la asignatura de destino (HU-22).
    /// Si el agente no las devuelve se usa la materia de origen de la entrada.
    /// </summary>
    public List<string>? MateriasOrigen { get; set; }
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

    public Task<ResultadoAgente> AnalizarAsignaturaAsync(
        string asignaturaOrigen, string contenidoOrigen, string asignaturaDestino, string contenidoDestino) =>
        AnalizarAsignaturaAsync(
            asignaturaOrigen, contenidoOrigen, asignaturaDestino, contenidoDestino, null);

    /// <summary>
    /// Compara la asignatura de origen con la de destino. Cuando se envían varias materias
    /// aprobadas, el agente puede proponer que dos de ellas cubran juntas la de destino (HU-22).
    /// </summary>
    public async Task<ResultadoAgente> AnalizarAsignaturaAsync(
        string asignaturaOrigen,
        string contenidoOrigen,
        string asignaturaDestino,
        string contenidoDestino,
        IReadOnlyList<string>? materiasPermitidas)
    {
        var permitidas = (materiasPermitidas ?? new List<string>())
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => m.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (permitidas.Count == 0)
            permitidas.Add(asignaturaOrigen);

        var listaPermitidas = string.Join(", ", permitidas);

        // 4. Usar el campo "system" y "format" de Ollama
        var systemPrompt = "Eres un experto en homologación académica de la Universidad Técnica de Ambato. " +
                           "Debes comparar asignaturas y devolver ÚNICAMENTE un JSON válido con la estructura especificada. " +
                           "Cuando ninguna materia de origen cubre por sí sola la asignatura de destino, puedes proponer " +
                           "que DOS materias de origen la cubran juntas; los porcentajes se calculan sobre el conjunto.";

        var userPrompt = $@"
        Compara la asignatura de origen con la de destino y devuelve un JSON con esta estructura exacta:
        {{
        ""materiasOrigen"": [""(uno o dos nombres de las materias de origen permitidas)""],
        ""contenido"": (número del 0 al 100),
        ""profundidad"": (número del 0 al 100),
        ""horas"": (número del 0 al 100),
        ""justificacion"": ""(una breve explicación)""
        }}

        Reglas:
        - ""materiasOrigen"" debe tener una o dos materias, elegidas SOLO de esta lista: {listaPermitidas}.
        - Nunca incluyas más de dos materias de origen.
        - Si una sola materia cubre la asignatura de destino, devuelve solo esa materia.
        - No agregues texto fuera del JSON.

        Asignatura Origen principal: {asignaturaOrigen}
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

                // Algunos modelos agregan texto antes o después del JSON: se recorta el objeto.
                var inicio = jsonLimpio.IndexOf('{');
                var fin = jsonLimpio.LastIndexOf('}');
                if (inicio >= 0 && fin > inicio)
                    jsonLimpio = jsonLimpio.Substring(inicio, fin - inicio + 1);

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

                // Validar la combinación: máximo dos materias y todas dentro de las permitidas.
                var propuestas = (resultado.MateriasOrigen ?? new List<string>())
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Select(m => m.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (propuestas.Count == 0)
                    propuestas.Add(asignaturaOrigen.Trim());

                if (propuestas.Count > 2)
                    throw new JsonException("El agente propuso más de dos materias de origen.");

                var ajenas = propuestas
                    .Where(m => !permitidas.Contains(m, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                if (ajenas.Count > 0)
                    throw new JsonException(
                        $"El agente propuso materias que no están aprobadas ni preseleccionadas: {string.Join(", ", ajenas)}.");

                resultado.MateriasOrigen = propuestas;
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
                RequiereRevision = true,
                MateriasOrigen = new List<string> { asignaturaOrigen }
            };
        }
    }
}
