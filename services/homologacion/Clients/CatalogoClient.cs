using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HomologaUTA.Homologacion.Clients
{
    public class CatalogoClient : ICatalogoClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<CatalogoClient> _logger;
        private readonly bool _usarStub; // Bandera para usar datos de prueba si el catálogo no está listo

        public CatalogoClient(HttpClient httpClient, IConfiguration config, ILogger<CatalogoClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _usarStub = config.GetValue<bool>("UsarStubCatalogo", true);
            
            // URL base del catálogo (puerto 5006 según el docker-compose)
            _httpClient.BaseAddress = new Uri(config["CatalogoUrl"] ?? "http://localhost:5006/");
        }

        public async Task<PlanAnaliticoDto?> ObtenerPlanAnaliticoAsync(int asignaturaId)
        {
            if (_usarStub)
            {
                // STUB: Datos de ejemplo para que el agente pueda trabajar sin el Catálogo real
                _logger.LogInformation($"Usando STUB para plan analítico de asignatura {asignaturaId}");
                return new PlanAnaliticoDto
                {
                    AsignaturaId = asignaturaId,
                    Contenido = "Contenido de prueba del sílabo (Stub). Temas: Introducción, Conceptos básicos, Ejercicios.",
                    ResultadosAprendizaje = "El estudiante comprende los conceptos y resuelve problemas.",
                    FechaCarga = DateTime.UtcNow.ToString("O")
                };
            }

            try
            {
                var response = await _httpClient.GetAsync($"asignaturas/{asignaturaId}/plan-analitico");
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null; // No hay plan
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<PlanAnaliticoDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al consultar plan analítico de la asignatura {asignaturaId}");
                return null; // Si falla la red, tratamos como si no hubiera plan
            }
        }
    }
}