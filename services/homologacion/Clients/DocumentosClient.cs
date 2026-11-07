using System.Net.Http.Json;
using System.Text.Json;

namespace HomologaUTA.Homologacion.Clients;

/// <summary>El servicio de Documentos no respondió a tiempo.</summary>
public class DocumentosNoDisponibleException : Exception
{
    public DocumentosNoDisponibleException(string mensaje, Exception? inner = null) : base(mensaje, inner) { }
}

/// <summary>Documento procesado de una solicitud, con el texto que se extrajo del PDF.</summary>
public class DocumentoExtraidoDto
{
    public int Id { get; set; }
    public string TipoDocumento { get; set; } = string.Empty;
    public string NombreArchivo { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string? TextoExtraido { get; set; }
}

/// <summary>
/// Consulta los documentos procesados de una solicitud en el servicio de Documentos.
/// Se usa para enviar al agente externo el contenido real de la malla, el historial y los sílabos.
/// </summary>
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

    /// <summary>Documentos de la solicitud (vacío si todavía no se ha subido ninguno).</summary>
    public async Task<List<DocumentoExtraidoDto>> ObtenerPorSolicitudAsync(
        int solicitudId, CancellationToken ct = default)
    {
        try
        {
            var resultado = await _http.GetFromJsonAsync<List<DocumentoExtraidoDto>>(
                $"internal/documentos/solicitud/{solicitudId}", ct);
            return resultado ?? new List<DocumentoExtraidoDto>();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new DocumentosNoDisponibleException("El servicio de Documentos no respondió a tiempo.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            _logger.LogError(ex, "No se pudieron consultar los documentos de la solicitud {SolicitudId}", solicitudId);
            throw new DocumentosNoDisponibleException("No se pudieron consultar los documentos de la solicitud.", ex);
        }
    }
}
