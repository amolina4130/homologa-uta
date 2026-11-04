using System.Net.Http.Json;
using System.Text.Json;
using HomologaUTA.Homologacion.DTOs;

namespace HomologaUTA.Homologacion.Clients;

public class SolicitudesNoDisponibleException : Exception
{
    public SolicitudesNoDisponibleException(string mensaje, Exception? inner = null) : base(mensaje, inner) { }
}

public class SolicitudesClient
{
    private readonly HttpClient _http;
    private readonly ILogger<SolicitudesClient> _logger;

    public SolicitudesClient(HttpClient http, IConfiguration config, ILogger<SolicitudesClient> logger)
    {
        _http = http;
        _logger = logger;

        var url = config["Solicitudes:Url"]
                  ?? throw new InvalidOperationException("Falta la configuración Solicitudes:Url.");
        _http.BaseAddress = new Uri(url.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(config.GetValue<int?>("Solicitudes:TimeoutSegundos") ?? 10);
    }

    public async Task<List<SolicitudInfoDto>> ObtenerPorIdsAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var lista = ids.Distinct().ToList();
        if (lista.Count == 0)
            return new List<SolicitudInfoDto>();

        try
        {
            // ESTA ERA LA RUTA ORIGINAL (NO TOCAR)
            var resultado = await _http.GetFromJsonAsync<List<SolicitudInfoDto>>(
                $"internal/solicitudes?ids={string.Join(",", lista)}", ct);
            return resultado ?? new List<SolicitudInfoDto>();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new SolicitudesNoDisponibleException("El servicio de Solicitudes no respondió a tiempo.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            _logger.LogError(ex, "No se pudo consultar el servicio de Solicitudes");
            throw new SolicitudesNoDisponibleException("No se pudo consultar el servicio de Solicitudes.", ex);
        }
    }

    public async Task<List<SolicitudInfoDto>> ObtenerPendientesAsync(CancellationToken ct = default)
    {
        try
        {
            // LA RUTA CORRECTA (con el prefijo del controlador)
            var resultado = await _http.GetFromJsonAsync<List<SolicitudInfoDto>>(
                "solicitudes/internal/solicitudes/pendientes", ct);
            return resultado ?? new List<SolicitudInfoDto>();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            _logger.LogError(ex, "No se pudo consultar las solicitudes pendientes");
            throw new SolicitudesNoDisponibleException("No se pudo consultar las solicitudes pendientes.", ex);
        }
    }
}