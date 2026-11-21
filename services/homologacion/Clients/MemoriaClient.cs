using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace HomologaUTA.Homologacion.Clients;

public class MemoriaClient : IMemoriaClient
{
    private readonly HttpClient _http;
    private readonly ILogger<MemoriaClient> _logger;
    private readonly bool _usarStub;

    public MemoriaClient(HttpClient http, IConfiguration config, ILogger<MemoriaClient> logger)
    {
        _http = http;
        _logger = logger;
        _usarStub = config.GetValue<bool>("UsarStubMemoria", true);
        
        // URL del catálogo (puerto 5006)
        _http.BaseAddress = new Uri(config["CatalogoUrl"] ?? "http://localhost:5006/");
    }

    public async Task<MemoriaEstadoDto?> ObtenerEstadoMemoriaAsync(CancellationToken ct = default)
    {
        if (_usarStub)
        {
            // STUB: Simulamos que la memoria ya está cargada con una huella
            _logger.LogInformation("Usando STUB para el estado de la memoria.");
            return new MemoriaEstadoDto
            {
                Huella = "hash-de-prueba-12345",
                FechaCarga = DateTime.UtcNow.AddDays(-1),
                TotalCarreras = 5,
                TotalAsignaturas = 234
            };
        }

        try
        {
            var response = await _http.GetAsync("memoria/estado", ct);
            
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null; // Sin memoria cargada

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<MemoriaEstadoDto>(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al consultar el estado de la memoria del Catálogo.");
            return null;
        }
    }
}