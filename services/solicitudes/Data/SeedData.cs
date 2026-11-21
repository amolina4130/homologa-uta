using System.Text.Json;
using System.Text.Json.Serialization;
using HomologaUTA.Solicitudes.Models;

namespace HomologaUTA.Solicitudes.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(SolicitudesDbContext context)
        {
            // 1. Si ya hay carreras, no hacemos nada (evita duplicados)
            if (context.Carreras.Any())
            {
                return;
            }

            // Configuración para que C# no sea sensible a mayúsculas/minúsculas en el JSON
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            // 2. Leer y cargar las Carreras
            var carrerasPath = Path.Combine(AppContext.BaseDirectory, "Data", "carreras.json");
            if (File.Exists(carrerasPath))
            {
                var carrerasJson = await File.ReadAllTextAsync(carrerasPath);
                var carreras = JsonSerializer.Deserialize<List<Carrera>>(carrerasJson, jsonOptions);

                if (carreras != null && carreras.Any())
                {
                    // Le decimos a SQL Server que genere los IDs automáticamente
                    foreach (var carrera in carreras)
                    {
                        carrera.Id = 0; 
                    }

                    await context.Carreras.AddRangeAsync(carreras);
                    await context.SaveChangesAsync(); // Guardamos primero para generar los IDs
                }
            }

            // 3. Leer y cargar las Asignaturas
            var asignaturasPath = Path.Combine(AppContext.BaseDirectory, "Data", "asignaturas.json");
            if (File.Exists(asignaturasPath))
            {
                var asignaturasJson = await File.ReadAllTextAsync(asignaturasPath);
                
                var asignaturas = JsonSerializer.Deserialize<List<Asignatura>>(asignaturasJson, jsonOptions);

                if (asignaturas != null && asignaturas.Any())
                {
                    foreach (var asignatura in asignaturas)
                    {
                        asignatura.Carrera = null!; 
                        asignatura.Id = 0; // <--- LÍNEA NUEVA PARA EVITAR EL ERROR DE IDENTITY
                    }

                    await context.Asignaturas.AddRangeAsync(asignaturas);
                    await context.SaveChangesAsync();
                }
            }
        }
    }
}