using System.Net.Http.Json;
using System.Text.Json;
using HomologaUTA.Homologacion.DTOs;

namespace HomologaUTA.Homologacion.Connectors;

/// <summary>El agente externo no respondió dentro del tiempo de espera.</summary>
public class AgenteExternoTimeoutException : Exception
{
    public AgenteExternoTimeoutException(int segundos)
        : base($"El agente externo no respondió en {segundos} segundos (tiempo de espera agotado).") { }
}

/// <summary>El agente externo no se pudo contactar o su respuesta no cumple el contrato.</summary>
public class AgenteExternoException : Exception
{
    public AgenteExternoException(string mensaje, Exception? inner = null) : base(mensaje, inner) { }
}

/// <summary>
/// Conector con el agente externo (HU-09). Envía la documentación y devuelve su respuesta tal cual:
/// no le impone reglas ni umbrales a la decisión.
/// </summary>
public class AgenteExternoConnector
{
    private readonly HttpClient _http;
    private readonly string _url;
    private readonly int _timeoutSegundos;
    private readonly ILogger<AgenteExternoConnector> _logger;

    public AgenteExternoConnector(HttpClient http, IConfiguration config, ILogger<AgenteExternoConnector> logger)
    {
        _http = http;
        _logger = logger;
        _url = config["AgenteExterno:Url"]
               ?? throw new InvalidOperationException("Falta la configuración AgenteExterno:Url.");
        _timeoutSegundos = config.GetValue<int?>("AgenteExterno:TimeoutSegundos") ?? 30;

        // El tiempo de espera lo controla el token de cada llamada, no el HttpClient
        _http.Timeout = Timeout.InfiniteTimeSpan;
    }

    public async Task<AgenteExternoResponse> EnviarDocumentacionAsync(
        AgenteExternoRequest request, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSegundos));

        try
        {
            using var respuesta = await _http.PostAsJsonAsync(_url, request, cts.Token);

            if (!respuesta.IsSuccessStatusCode)
                throw new AgenteExternoException(
                    $"El agente externo respondió con error {(int)respuesta.StatusCode}.");

            var resultado = await respuesta.Content.ReadFromJsonAsync<AgenteExternoResponse>(cts.Token);

            if (resultado is null || !resultado.AnalisisCompletado || resultado.Asignaturas.Count == 0)
                throw new AgenteExternoException(
                    "El agente externo no completó el análisis o no devolvió asignaturas.");

            return resultado;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Tiempo de espera agotado con el agente externo (solicitud {SolicitudId})", request.SolicitudId);
            throw new AgenteExternoTimeoutException(_timeoutSegundos);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "No se pudo contactar al agente externo (solicitud {SolicitudId})", request.SolicitudId);
            throw new AgenteExternoException("No se pudo contactar al agente externo.", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Respuesta del agente externo con formato inválido (solicitud {SolicitudId})", request.SolicitudId);
            throw new AgenteExternoException("La respuesta del agente externo no tiene el formato del contrato.", ex);
        }
    }
}