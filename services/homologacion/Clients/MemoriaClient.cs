using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HomologaUTA.Homologacion.DTOs;

namespace HomologaUTA.Homologacion.Clients;

/// <summary>La memoria del agente todavía no se cargó en el Catálogo.</summary>
public class MemoriaSinCargarException : Exception
{
    public MemoriaSinCargarException()
        : base("Memoria del agente sin cargar") { }
}

/// <summary>El servicio de Catálogo/Solicitudes no respondió.</summary>
public class MemoriaNoDisponibleException : Exception
{
    public MemoriaNoDisponibleException(string mensaje, Exception? inner = null) : base(mensaje, inner) { }
}

/// <summary>
/// Consulta la memoria relacional del agente por API (HU-19/HU-21). Nunca accede a la base
/// de datos del Catálogo: solo usa los endpoints de memoria.
/// </summary>
public interface IMemoriaClient
{
    Task<MemoriaEstadoDto?> ObtenerEstadoAsync(CancellationToken ct = default);

    Task<List<MemoriaAsignaturaDto>> ObtenerAsignaturasAsync(
        int carreraId, int? nivelMaximo = null, CancellationToken ct = default);

    /// <summary>Verifica que la memoria exista y devuelve su estado; 409 si no está cargada.</summary>
    Task<MemoriaEstadoDto> VerificarAsync(CancellationToken ct = default);
}

public class MemoriaClient : IMemoriaClient
{
    private readonly HttpClient _http;
    private readonly ILogger<MemoriaClient> _logger;

    public MemoriaClient(HttpClient http, IConfiguration config, ILogger<MemoriaClient> logger)
    {
        _http = http;
        _logger = logger;

        var url = config["Memoria:Url"] ?? config["Solicitudes:Url"]
                  ?? throw new InvalidOperationException("Falta la configuración Memoria:Url.");

        _http.BaseAddress = new Uri(url.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(config.GetValue<int?>("Memoria:TimeoutSegundos") ?? 30);
    }

    public async Task<MemoriaEstadoDto?> ObtenerEstadoAsync(CancellationToken ct = default)
    {
        try
        {
            using var respuesta = await _http.GetAsync("memoria/estado", ct);
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
                return null;

            respuesta.EnsureSuccessStatusCode();
            return await respuesta.Content.ReadFromJsonAsync<MemoriaEstadoDto>(cancellationToken: ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new MemoriaNoDisponibleException("El servicio de memoria no respondió a tiempo.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            _logger.LogError(ex, "No se pudo consultar el estado de la memoria del agente");
            throw new MemoriaNoDisponibleException("No se pudo consultar la memoria del agente.", ex);
        }
    }

    public async Task<List<MemoriaAsignaturaDto>> ObtenerAsignaturasAsync(
        int carreraId, int? nivelMaximo = null, CancellationToken ct = default)
    {
        var url = $"memoria/carreras/{carreraId}/asignaturas";
        if (nivelMaximo is > 0)
            url += $"?nivelMaximo={nivelMaximo}";

        try
        {
            var resultado = await _http.GetFromJsonAsync<List<MemoriaAsignaturaDto>>(url, ct);
            return resultado ?? new List<MemoriaAsignaturaDto>();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new MemoriaNoDisponibleException("El servicio de memoria no respondió a tiempo.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            _logger.LogError(ex, "No se pudo consultar la memoria de la carrera {CarreraId}", carreraId);
            throw new MemoriaNoDisponibleException("No se pudo consultar la memoria del agente.", ex);
        }
    }

    public async Task<MemoriaEstadoDto> VerificarAsync(CancellationToken ct = default)
    {
        var estado = await ObtenerEstadoAsync(ct)
                     ?? throw new MemoriaSinCargarException();
        return estado;
    }
}
