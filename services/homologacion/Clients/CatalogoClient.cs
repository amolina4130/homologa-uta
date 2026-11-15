using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HomologaUTA.Homologacion.DTOs;

namespace HomologaUTA.Homologacion.Clients;

/// <summary>
/// Acceso al Catálogo (servicio de Solicitudes) por API (HU-13, HU-14, HU-19).
/// El servicio de Homologación nunca lee la base de datos del Catálogo directamente.
/// </summary>
public interface ICatalogoClient
{
    Task<PlanAnaliticoDto?> ObtenerPlanAnaliticoAsync(int asignaturaId, CancellationToken ct = default);
    Task<MemoriaEstadoDto?> ObtenerEstadoMemoriaAsync(CancellationToken ct = default);
    Task<List<MemoriaAsignaturaDto>> ObtenerAsignaturasMemoriaAsync(int carreraId, int? nivelMaximo, CancellationToken ct = default);
    Task<List<EquivalenciaDto>> ObtenerEquivalenciasAsync(int carreraOrigenId, int carreraDestinoId, CancellationToken ct = default);
}

/// <summary>El Catálogo no respondió o devolvió una respuesta que no cumple el contrato.</summary>
public class CatalogoNoDisponibleException : Exception
{
    public CatalogoNoDisponibleException(string mensaje, Exception? inner = null) : base(mensaje, inner) { }
}

public class CatalogoClient : ICatalogoClient
{
    private readonly HttpClient _http;
    private readonly ILogger<CatalogoClient> _logger;

    public CatalogoClient(HttpClient http, IConfiguration config, ILogger<CatalogoClient> logger)
    {
        _http = http;
        _logger = logger;

        var url = config["Catalogo:Url"] ?? config["Solicitudes:Url"]
                  ?? throw new InvalidOperationException("Falta la configuración Catalogo:Url.");
        _http.BaseAddress = new Uri(url.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(config.GetValue<int?>("Catalogo:TimeoutSegundos") ?? 10);
    }

    public async Task<PlanAnaliticoDto?> ObtenerPlanAnaliticoAsync(int asignaturaId, CancellationToken ct = default)
    {
        try
        {
            using var respuesta = await _http.GetAsync($"asignaturas/{asignaturaId}/plan-analitico", ct);
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
                return null;
            respuesta.EnsureSuccessStatusCode();
            return await respuesta.Content.ReadFromJsonAsync<PlanAnaliticoDto>(cancellationToken: ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new CatalogoNoDisponibleException("El Catálogo no respondió a tiempo.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            _logger.LogError(ex, "No se pudo consultar el plan analítico de la asignatura {AsignaturaId}", asignaturaId);
            throw new CatalogoNoDisponibleException("No se pudo consultar el Catálogo.", ex);
        }
    }

    public async Task<MemoriaEstadoDto?> ObtenerEstadoMemoriaAsync(CancellationToken ct = default)
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
            throw new CatalogoNoDisponibleException("El Catálogo no respondió a tiempo.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            _logger.LogError(ex, "No se pudo consultar el estado de la memoria del agente");
            throw new CatalogoNoDisponibleException("No se pudo consultar el Catálogo.", ex);
        }
    }

    public async Task<List<MemoriaAsignaturaDto>> ObtenerAsignaturasMemoriaAsync(
        int carreraId, int? nivelMaximo, CancellationToken ct = default)
    {
        var url = $"memoria/carreras/{carreraId}/asignaturas";
        if (nivelMaximo.HasValue) url += $"?nivelMaximo={nivelMaximo.Value}";

        try
        {
            return await _http.GetFromJsonAsync<List<MemoriaAsignaturaDto>>(url, ct)
                   ?? new List<MemoriaAsignaturaDto>();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new CatalogoNoDisponibleException("El Catálogo no respondió a tiempo.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            _logger.LogError(ex, "No se pudo consultar la memoria de la carrera {CarreraId}", carreraId);
            throw new CatalogoNoDisponibleException("No se pudo consultar el Catálogo.", ex);
        }
    }

    public async Task<List<EquivalenciaDto>> ObtenerEquivalenciasAsync(
        int carreraOrigenId, int carreraDestinoId, CancellationToken ct = default)
    {
        var url = $"equivalencias?carreraOrigenId={carreraOrigenId}&carreraDestinoId={carreraDestinoId}&activa=true";
        try
        {
            return await _http.GetFromJsonAsync<List<EquivalenciaDto>>(url, ct)
                   ?? new List<EquivalenciaDto>();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new CatalogoNoDisponibleException("El Catálogo no respondió a tiempo.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            _logger.LogError(ex, "No se pudo consultar las equivalencias");
            throw new CatalogoNoDisponibleException("No se pudo consultar el Catálogo.", ex);
        }
    }
}
