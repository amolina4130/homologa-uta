using Microsoft.AspNetCore.Mvc;
using HomologaUTA.Homologacion.Clients;

namespace HomologaUTA.Homologacion.Controllers;

[ApiController]
[Route("prueba-ollama")]
public class PruebaOllamaController : ControllerBase
{
    private readonly OllamaClient _ollamaClient;

    public PruebaOllamaController(OllamaClient ollamaClient)
    {
        _ollamaClient = ollamaClient;
    }

    [HttpGet]
    public async Task<IActionResult> ProbarOllama()
    {
        var prompt = "Responde solo con la palabra 'Funciona' si estás conectado.";
        var respuesta = await _ollamaClient.GenerarRespuestaAsync(prompt);
        
        return Ok(new { mensaje = "Conexión exitosa con Ollama", respuestaDelModelo = respuesta });
    }
}