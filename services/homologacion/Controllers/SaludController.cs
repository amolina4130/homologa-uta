using System.Net.Sockets;
using Microsoft.AspNetCore.Mvc;

namespace HomologaUTA.Homologacion.Controllers;

/// <summary>
/// Estado real de los componentes externos usados por el agente. El Gateway expone estas
/// rutas de forma anónima bajo /salud para que el panel de estado pueda consultarlas.
/// </summary>
[ApiController]
[Route("salud")]
public class SaludController : ControllerBase
{
    private const int PuertoRabbitMq = 5672;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public SaludController(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    /// <summary>Comprueba si Ollama responde en /api/tags.</summary>
    [HttpGet("ollama")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Ollama()
    {
        var url = (_config["Ollama:Url"] ?? "http://ollama:11434").TrimEnd('/');
        var modelo = _config["OLLAMA_MODEL"] ?? _config["Ollama:Model"] ?? "llama3";

        try
        {
            var cliente = _httpClientFactory.CreateClient();
            cliente.Timeout = TimeSpan.FromSeconds(5);

            using var respuesta = await cliente.GetAsync($"{url}/api/tags");
            var enLinea = respuesta.IsSuccessStatusCode;
            var detalle = enLinea
                ? "Ollama responde correctamente."
                : $"Ollama devolvió el estado {(int)respuesta.StatusCode}.";

            return Ok(new { nombre = "Ollama (agente)", enLinea, modelo, detalle });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                nombre = "Ollama (agente)",
                enLinea = false,
                modelo,
                detalle = $"No se pudo contactar a Ollama: {ex.Message}"
            });
        }
    }

    /// <summary>Comprueba el puerto AMQP de RabbitMQ mediante TCP.</summary>
    [HttpGet("rabbitmq")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RabbitMq()
    {
        var host = _config["RabbitMQ:Host"] ?? "rabbitmq";

        try
        {
            using var cliente = new TcpClient();
            using var cancelacion = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await cliente.ConnectAsync(host, PuertoRabbitMq, cancelacion.Token);

            return Ok(new { nombre = "RabbitMQ", enLinea = cliente.Connected, puerto = PuertoRabbitMq });
        }
        catch (Exception)
        {
            return Ok(new { nombre = "RabbitMQ", enLinea = false, puerto = PuertoRabbitMq });
        }
    }
}
