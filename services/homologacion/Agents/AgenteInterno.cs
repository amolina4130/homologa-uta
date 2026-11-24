using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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
    /// <summary>
    /// Tiempo máximo de una llamada al modelo. Si se pasa, se usa la comparación determinista
    /// de la memoria del agente para que el análisis nunca supere los 10 segundos.
    /// </summary>
    private const int TimeoutOllamaSegundos = 6;

    private readonly Clients.OllamaClient _ollamaClient;
    private readonly ILogger<AgenteInterno> _logger;

    public AgenteInterno(Clients.OllamaClient ollamaClient, ILogger<AgenteInterno> logger)
    {
        _ollamaClient = ollamaClient;
        _logger = logger;
    }

    public Task<ResultadoAgente> AnalizarAsignaturaAsync(
        string asignaturaOrigen, string contenidoOrigen, string asignaturaDestino, string contenidoDestino) =>
        AnalizarAsignaturaAsync(
            asignaturaOrigen, contenidoOrigen, asignaturaDestino, contenidoDestino, null, true);

    /// <summary>
    /// Compara la asignatura de origen con la de destino. Cuando se envían varias materias
    /// aprobadas, el agente puede proponer que dos de ellas cubran juntas la de destino (HU-22).
    /// </summary>
    public async Task<ResultadoAgente> AnalizarAsignaturaAsync(
        string asignaturaOrigen,
        string contenidoOrigen,
        string asignaturaDestino,
        string contenidoDestino,
        IReadOnlyList<string>? materiasPermitidas,
        bool usarOllama = true)
    {
        var permitidas = (materiasPermitidas ?? new List<string>())
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => m.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (permitidas.Count == 0)
            permitidas.Add(asignaturaOrigen);

        var listaPermitidas = string.Join(", ", permitidas);
        var comparacionMemoria = CalcularConMemoria(
            asignaturaOrigen, contenidoOrigen, asignaturaDestino, contenidoDestino);

        // El registro de una solicitud solo usa la memoria del agente: es instantáneo y no
        // depende de que el modelo esté respondiendo. La IA se pide explícitamente en la
        // sugerencia del Docente o en la recomendación bajo demanda.
        if (!usarOllama)
            return comparacionMemoria;

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

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutOllamaSegundos));
        try
        {
            var promptCompleto = $"{systemPrompt}\n\n{userPrompt}";
            var respuestaRaw = await _ollamaClient.GenerarRespuestaAsync(promptCompleto, cts.Token, 160);
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
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Ollama tardó más de {Segundos}s en {Asignatura}; se usa la memoria del agente.",
                TimeoutOllamaSegundos, asignaturaOrigen);
            return comparacionMemoria;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Ollama no devolvió un JSON válido para {Asignatura}; se usa la memoria del agente.",
                asignaturaOrigen);
            return comparacionMemoria;
        }
    }

    /// <summary>
    /// Comparación determinista con la memoria del agente. Se usa siempre en el análisis del
    /// registro y como respaldo cuando el modelo no responde a tiempo.
    /// </summary>
    private static ResultadoAgente CalcularConMemoria(
        string asignaturaOrigen, string contenidoOrigen,
        string asignaturaDestino, string contenidoDestino)
    {
        var tokensOrigen = ObtenerTokens($"{asignaturaOrigen} {contenidoOrigen}");
        var tokensDestino = ObtenerTokens($"{asignaturaDestino} {contenidoDestino}");

        var comunes = tokensOrigen.Intersect(tokensDestino).Count();
        var totales = tokensOrigen.Union(tokensDestino).Count();
        var porcentaje = totales == 0 ? 0 : (int)Math.Round(100.0 * comunes / totales);

        if (Normalizar(asignaturaOrigen) == Normalizar(asignaturaDestino))
            porcentaje = Math.Max(porcentaje, 95);

        porcentaje = Math.Clamp(porcentaje, 0, 100);
        var cumple = porcentaje >= 80;

        return new ResultadoAgente
        {
            Contenido = porcentaje,
            Profundidad = porcentaje,
            Horas = porcentaje,
            Justificacion = cumple
                ? $"Comparación con la memoria del agente interno: contenido, profundidad y carga horaria coinciden en {porcentaje}%."
                : $"Comparación con la memoria del agente interno: solo {porcentaje}% de coincidencia; no alcanza el umbral del 80%.",
            RequiereRevision = false,
            MateriasOrigen = new List<string> { asignaturaOrigen.Trim() }
        };
    }

    private static HashSet<string> ObtenerTokens(string texto) =>
        Normalizar(texto)
            .Split(new[] { ' ', ';', ',', '.', '(', ')', '/', '-', '_', '\n', '\r', '\t' },
                StringSplitOptions.RemoveEmptyEntries)
            .Where(palabra => palabra.Length > 2)
            .ToHashSet();

    private static string Normalizar(string texto)
    {
        var descompuesto = (texto ?? string.Empty).Normalize(NormalizationForm.FormD);
        var sinTildes = new StringBuilder();
        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
                sinTildes.Append(caracter);
        }
        return sinTildes.ToString().ToLowerInvariant();
    }
}
