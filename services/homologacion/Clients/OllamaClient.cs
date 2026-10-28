using System.Text;
using System.Text.Json;

namespace HomologaUTA.Homologacion.Clients;

public class OllamaClient
{
    private readonly HttpClient _httpClient;
    private readonly string _ollamaUrl;

    public OllamaClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        // Leemos la URL de Ollama desde appsettings o usamos la de Docker por defecto
        _ollamaUrl = configuration["Ollama:Url"] ?? "http://ollama:11434";
    }

    public async Task<string> GenerarRespuestaAsync(string prompt)
    {
        var requestBody = new
        {
            model = "llama3",
            prompt = prompt,
            stream = false // Para que devuelva toda la respuesta junta
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_ollamaUrl}/api/generate", content);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Error al conectar con Ollama: {response.StatusCode}");
        }

        var responseString = await response.Content.ReadAsStringAsync();
        
        // Parseamos la respuesta para obtener solo el texto generado
        using var doc = JsonDocument.Parse(responseString);
        return doc.RootElement.GetProperty("response").GetString() ?? string.Empty;
    }
}