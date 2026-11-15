using System.Net.Http.Json;
using System.Text.Json;
using HomologaUTA.Homologacion.DTOs;

namespace HomologaUTA.Homologacion.Clients;

/// <summary>El servicio de Documentos no respondió o devolvió una respuesta inválida.</summary>
public class DocumentosNoDisponibleException : Exception
{
    public DocumentosNoDisponibleException(string mensaje, Exception? inner = null) : base(mensaje, inner) { }
}

/// <summary>Consulta el texto extraído de los documentos de una solicitud (HU-20).</summary>
public class DocumentosClient
{
    private readonly HttpClient _http;
    private readonly ILogger<DocumentosClient> _logger;

    public DocumentosClient(HttpClient http, IConfiguration config, ILogger<DocumentosClient> logger)
    {
        _http = http;
        _logger = logger;

        var url = config["Documentos:Url"]
                  ?? throw new InvalidOperationException("Falta la configuración Documentos:Url.");
        _http.BaseAddress = new Uri(url.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(config.GetValue<int?>("Documentos:TimeoutSegundos") ?? 10);
    }

    public async Task<List<DocumentoTextoDto>> ObtenerTextosAsync(
        int solicitudId, string? tipo = null, CancellationToken ct = default)
    {
        var url = $"internal/documentos/solicitud/{solicitudId}";
        if (!string.IsNullOrWhiteSpace(tipo)) url += $"?tipo={Uri.EscapeDataString(tipo)}";

        try
        {
            return await _http.GetFromJsonAsync<List<DocumentoTextoDto>>(url, ct)
                   ?? new List<DocumentoTextoDto>();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new DocumentosNoDisponibleException("El servicio de Documentos no respondió a tiempo.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            _logger.LogError(ex, "No se pudo consultar los documentos de la solicitud {SolicitudId}", solicitudId);
            throw new DocumentosNoDisponibleException("No se pudo consultar el servicio de Documentos.", ex);
        }
    }
}
