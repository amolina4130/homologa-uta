using System.Net;
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

    /// <summary>Datos básicos de varias solicitudes.</summary>
    public async Task<List<SolicitudInfoDto>> ObtenerPorIdsAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var lista = ids.Distinct().ToList();
        if (lista.Count == 0)
            return new List<SolicitudInfoDto>();

        try
        {
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

    /// <summary>Solicitudes en estado PENDIENTE_REVISION.</summary>
    public async Task<List<SolicitudInfoDto>> ObtenerPendientesAsync(CancellationToken ct = default)
    {
        try
        {
            var resultado = await _http.GetFromJsonAsync<List<SolicitudInfoDto>>(
                "internal/solicitudes/pendientes", ct);
            return resultado ?? new List<SolicitudInfoDto>();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new SolicitudesNoDisponibleException("El servicio de Solicitudes no respondió a tiempo.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            _logger.LogError(ex, "No se pudo consultar las solicitudes pendientes");
            throw new SolicitudesNoDisponibleException("No se pudo consultar las solicitudes pendientes.", ex);
        }
    }

    /// <summary>
    /// Solicitud completa con sus asignaturas de origen y el equivalente en destino.
    /// Devuelve null si la solicitud no existe.
    /// </summary>
    public async Task<SolicitudCompletaDto?> ObtenerCompletaAsync(int id, CancellationToken ct = default)
    {
        try
        {
            using var respuesta = await _http.GetAsync($"internal/solicitudes/{id}/completa", ct);
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
                return null;

            respuesta.EnsureSuccessStatusCode();
            return await respuesta.Content.ReadFromJsonAsync<SolicitudCompletaDto>(cancellationToken: ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new SolicitudesNoDisponibleException("El servicio de Solicitudes no respondió a tiempo.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            _logger.LogError(ex, "No se pudo obtener la solicitud {SolicitudId}", id);
            throw new SolicitudesNoDisponibleException("No se pudo obtener la solicitud completa.", ex);
        }
    }

    /// <summary>
    /// Cambia el estado de la solicitud y registra el historial. Devuelve false si la
    /// solicitud no existe.
    /// </summary>
    public async Task<bool> CambiarEstadoAsync(
        int id, string estado, string usuario, CancellationToken ct = default)
    {
        try
        {
            using var respuesta = await _http.PostAsJsonAsync(
                $"internal/solicitudes/{id}/estado",
                new { estado, usuario },
                ct);

            if (respuesta.StatusCode == HttpStatusCode.NotFound)
                return false;

            respuesta.EnsureSuccessStatusCode();
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new SolicitudesNoDisponibleException("El servicio de Solicitudes no respondió a tiempo.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            _logger.LogError(ex, "No se pudo cambiar el estado de la solicitud {SolicitudId}", id);
            throw new SolicitudesNoDisponibleException("No se pudo actualizar el estado de la solicitud.", ex);
        }
    }
}
