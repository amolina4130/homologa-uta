using System.Text.Json;
using System.Text.Json.Serialization;
using HomologaUTA.Solicitudes.Models;
using HomologaUTA.Solicitudes.Services;

namespace HomologaUTA.Solicitudes.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(SolicitudesDbContext context)
        {
            // 1. Si ya hay carreras, no se vuelven a cargar (evita duplicados),
            //    pero sí se completan áreas y planes analíticos de la demostración.
            if (context.Carreras.Any())
            {
                await CompletarAreasYPlanesAsync(context);
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
                    await context.Carreras.AddRangeAsync(carreras);
                    await context.SaveChangesAsync(); // Guardamos primero para generar los IDs
                }
            }

            // 3. Leer y cargar las Asignaturas
            var asignaturasPath = Path.Combine(AppContext.BaseDirectory, "Data", "asignaturas.json");
            if (File.Exists(asignaturasPath))
            {
                var asignaturasJson = await File.ReadAllTextAsync(asignaturasPath);
                
                // Ignoramos la propiedad "Carrera" para que no intente crear una carrera nueva al insertar la asignatura
                var asignaturas = JsonSerializer.Deserialize<List<Asignatura>>(asignaturasJson, jsonOptions);

                if (asignaturas != null && asignaturas.Any())
                {
                    // Nos aseguramos de que la relación con Carrera sea nula para no causar conflictos
                    foreach (var asignatura in asignaturas)
                    {
                        asignatura.Carrera = null!; 
                    }

                    await context.Asignaturas.AddRangeAsync(asignaturas);
                    await context.SaveChangesAsync();
                }
            }

            await CompletarAreasYPlanesAsync(context);
        }

        /// <summary>
        /// Completa el área de las asignaturas existentes y crea un plan analítico de
        /// demostración para las que todavía no tienen uno (HU-13/HU-19).
        /// </summary>
        private static async Task CompletarAreasYPlanesAsync(SolicitudesDbContext context)
        {
            var asignaturas = context.Asignaturas.ToList();
            var cambio = false;

            foreach (var asignatura in asignaturas)
            {
                if (string.IsNullOrWhiteSpace(asignatura.Area))
                {
                    asignatura.Area = AreaAsignatura.Detectar(asignatura.Nombre);
                    cambio = true;
                }
            }

            if (cambio)
                await context.SaveChangesAsync();

            var conPlan = context.PlanesAnaliticos.Select(p => p.AsignaturaId).ToHashSet();
            foreach (var asignatura in asignaturas.Where(a => !conPlan.Contains(a.Id)))
            {
                context.PlanesAnaliticos.Add(new PlanAnalitico
                {
                    AsignaturaId = asignatura.Id,
                    Contenido = ContenidoDemostracion(asignatura),
                    ResultadosAprendizaje =
                        $"Al finalizar la asignatura el estudiante aplica los conceptos de {asignatura.Nombre} " +
                        $"en problemas propios del área de {asignatura.Area}.",
                    FechaCarga = DateTime.UtcNow
                });
            }

            await context.SaveChangesAsync();
        }

        private static string ContenidoDemostracion(Asignatura asignatura) =>
            $"Plan analítico de {asignatura.Nombre} ({asignatura.Nivel}, {asignatura.Creditos} créditos, " +
            $"{asignatura.Horas} horas). Área: {asignatura.Area}. Unidades: fundamentos, conceptos esenciales, " +
            $"aplicaciones y ejercicios de {asignatura.Nombre}.";
    }
}