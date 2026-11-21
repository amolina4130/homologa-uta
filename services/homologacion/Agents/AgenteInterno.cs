using System.Text.Json;
using System.Text.Json.Serialization;
using Polly;
using Polly.Retry;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using HomologaUTA.Homologacion.Clients;
using System.Threading.Tasks;

namespace HomologaUTA.Homologacion.Agents;

public class ResultadoAgente
{
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

    [JsonPropertyName("materiasOrigen")]
    public List<string> MateriasOrigen { get; set; } = new();
}

public class AgenteInterno
{
    private readonly Clients.OllamaClient _ollamaClient;
    private readonly ICatalogoClient _catalogoClient; // <-- NUEVO CLIENTE
    private readonly ResiliencePipeline _retryPipeline;
    private readonly ILogger<AgenteInterno> _logger;

    // Inyectamos el ICatalogoClient en el constructor
    public AgenteInterno(Clients.OllamaClient ollamaClient, ICatalogoClient catalogoClient, ILogger<AgenteInterno> logger)
    {
        _ollamaClient = ollamaClient;
        _catalogoClient = catalogoClient;
        _logger = logger;
        
        _retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                ShouldHandle = new PredicateBuilder()
                    .Handle<JsonException>()
                    .Handle<TaskCanceledException>()
                    .Handle<HttpRequestException>(),
                OnRetry = args =>
                {
                    _logger.LogWarning("Reintento {Attempt} por error: {Exception}", args.AttemptNumber, args.Outcome.Exception?.Message);
                    return default;
                }
            })
            .Build();
    }

    // Ahora recibe los IDs en lugar del texto
    public async Task<ResultadoAgente> AnalizarAsignaturaAsync(int idOrigen, string nombreOrigen, int idDestino, string nombreDestino)
    {
        // 1. Consultar los planes analíticos usando el Cliente (Stub o real)
        var planOrigen = await _catalogoClient.ObtenerPlanAnaliticoAsync(idOrigen);
        var planDestino = await _catalogoClient.ObtenerPlanAnaliticoAsync(idDestino);

        // 2. Si falta alguno, requiere revisión (Criterio de aceptación)
        if (planOrigen == null || planDestino == null)
        {
            _logger.LogWarning("Falta plan analítico para {Origen} o {Destino}", nombreOrigen, nombreDestino);
            return new ResultadoAgente
            {
                Contenido = 0, Profundidad = 0, Horas = 0,
                Justificacion = "Falta el plan analítico de una o ambas asignaturas en el Catálogo. Requiere revisión.",
                RequiereRevision = true
            };
        }

        // 3. Armar los prompts con el contenido REAL
        var systemPrompt = "Eres un experto en homologación académica de la Universidad Técnica de Ambato. " +
                           "Debes comparar asignaturas y devolver ÚNICAMENTE un JSON válido con la estructura especificada.";
                           
        var userPrompt = $@"
        Compara la siguiente asignatura de origen con la de destino.
        Si una sola materia de origen no cubre la de destino, pero la combinación de DOS materias sí, 
        devuelve las dos materias en ""materiasOrigen"" y calcula los porcentajes sobre el conjunto.
        Devuelve un JSON con esta estructura exacta:
        {{
        ""materiasOrigen"": [""Nombre de materia 1"", ""Nombre de materia 2""],
        ""contenido"": (número del 0 al 100),
        ""profundidad"": (número del 0 al 100),
        ""horas"": (número del 0 al 100),
        ""justificacion"": ""(una breve explicación)""
        }}

        No agregues texto fuera del JSON.

        Asignatura Origen: {nombreOrigen}
        Contenido: {planOrigen.Contenido}
        Resultados: {planOrigen.ResultadosAprendizaje}

        Asignatura Destino: {nombreDestino}
        Contenido: {planDestino.Contenido}
        Resultados: {planDestino.ResultadosAprendizaje}
        ";

        try
        {
            return await _retryPipeline.ExecuteAsync(async token =>
            {
                var promptCompleto = $"{systemPrompt}\n\n{userPrompt}";
                var respuestaRaw = await _ollamaClient.GenerarRespuestaAsync(promptCompleto);
                var jsonLimpio = respuestaRaw.Replace("```json", "").Replace("```", "").Trim();

                var inicio = jsonLimpio.IndexOf('{');
                var fin = jsonLimpio.LastIndexOf('}');
                if (inicio >= 0 && fin > inicio)
                    jsonLimpio = jsonLimpio.Substring(inicio, fin - inicio + 1);

                var opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var resultado = JsonSerializer.Deserialize<ResultadoAgente>(jsonLimpio, opciones);

                if (resultado.MateriasOrigen.Count > 2)
                    throw new JsonException("El agente propuso más de dos materias de origen.");

                if (resultado == null)
                    throw new JsonException("El JSON devuelto está vacío.");

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
            _logger.LogError(ex, "El agente falló tras los reintentos para la asignatura {Asignatura}", nombreOrigen);
            
            return new ResultadoAgente
            {
                Contenido = 0, Profundidad = 0, Horas = 0,
                Justificacion = "El agente no pudo generar una respuesta válida tras varios intentos. Requiere revisión manual.",
                RequiereRevision = true
            };
        }
    }
}