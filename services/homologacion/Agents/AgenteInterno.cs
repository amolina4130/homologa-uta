using System.Text.Json;
using Polly;
using Polly.Retry;

namespace HomologaUTA.Homologacion.Agents;

public class ResultadoAgente
{
    public int Contenido { get; set; }
    public int Profundidad { get; set; }
    public int Horas { get; set; }
    public string Justificacion { get; set; } = string.Empty;
    public bool RequiereRevision { get; set; } = false;
}

public class AgenteInterno
{
    private readonly Clients.OllamaClient _ollamaClient;
    private readonly AsyncRetryPolicy _retryPolicy;

    public AgenteInterno(Clients.OllamaClient ollamaClient)
    {
        _ollamaClient = ollamaClient;
        
        // Configuración de Polly: 3 reintentos, esperando 2 segundos entre cada uno
        _retryPolicy = Policy
            .Handle<JsonException>()
            .Or<TimeoutException>()
            .Or<Exception>(ex => ex.Message.Contains("Error al conectar con Ollama"))
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(2));
    }

    public async Task<ResultadoAgente> AnalizarAsignaturaAsync(string asignaturaOrigen, string contenidoOrigen, string asignaturaDestino, string contenidoDestino)
    {
        // 1. Definimos el Prompt de Sistema (Criterio 1)
        var prompt = $@"
Eres un experto en homologación académica de la Universidad Técnica de Ambato.
Compara la siguiente asignatura de origen con la de destino y devuelve ÚNICAMENTE un JSON válido, sin texto adicional, sin markdown, con esta estructura exacta:
{{
  ""contenido"": (número del 0 al 100),
  ""profundidad"": (número del 0 al 100),
  ""horas"": (número del 0 al 100),
  ""justificacion"": ""(una breve explicación de por qué asignaste esos porcentajes)""
}}

Asignatura Origen: {asignaturaOrigen}
Contenido: {contenidoOrigen}

Asignatura Destino: {asignaturaDestino}
Contenido: {contenidoDestino}
";

        try
        {
            // 2. Ejecutamos con Polly (Criterio 4)
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var respuestaRaw = await _ollamaClient.GenerarRespuestaAsync(prompt);
                
                // 3. Validamos el JSON con System.Text.Json (Criterio 2 y 3)
                // Limpiamos posibles bloques de markdown que el modelo a veces añade
                var jsonLimpio = respuestaRaw.Replace("```json", "").Replace("```", "").Trim();
                
                var opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var resultado = JsonSerializer.Deserialize<ResultadoAgente>(jsonLimpio, opciones);

                if (resultado == null)
                    throw new JsonException("El JSON devuelto está vacío.");

                return resultado;
            });
        }
        catch (Exception)
        {
            // 4. Si persiste el fallo tras los reintentos, queda "requiere revisión" (Criterio 5)
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