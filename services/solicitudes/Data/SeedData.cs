using System.Text.Json;
using HomologaUTA.Solicitudes.Models;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Solicitudes.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(SolicitudesDbContext context)
        {
            // Configuración para que C# no sea sensible a mayúsculas/minúsculas en el JSON
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            // 1. Carreras y asignaturas: solo si la base está vacía
            if (!await context.Carreras.AnyAsync())
            {
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

                var asignaturasPath = Path.Combine(AppContext.BaseDirectory, "Data", "asignaturas.json");
                if (File.Exists(asignaturasPath))
                {
                    var asignaturasJson = await File.ReadAllTextAsync(asignaturasPath);
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
            }

            await SembrarPlanesAsync(context);
            await SembrarEquivalenciasAsync(context);
        }

        /// <summary>
        /// Plan analítico de demostración para cada asignatura (HU-13). Sin él la memoria del agente
        /// no tendría contenidos mínimos con los que comparar.
        /// </summary>
        private static async Task SembrarPlanesAsync(SolicitudesDbContext context)
        {
            if (await context.PlanesAnaliticos.AnyAsync())
                return;

            var asignaturas = await context.Asignaturas.AsNoTracking().ToListAsync();
            if (asignaturas.Count == 0)
                return;

            var planes = asignaturas.Select(a => new PlanAnalitico
            {
                AsignaturaId = a.Id,
                Contenido = $"""
                    PLAN ANALÍTICO
                    Asignatura: {a.Nombre}
                    Nivel: {a.Nivel}
                    Créditos: {a.Creditos}
                    Horas: {a.Horas}

                    CONTENIDOS MÍNIMOS
                    - Fundamentos y conceptos básicos de {a.Nombre}.
                    - Métodos, técnicas y aplicaciones de {a.Nombre}.
                    - Ejercicios, prácticas y resolución de problemas de {a.Nombre}.
                    - Integración de {a.Nombre} con asignaturas del mismo nivel.

                    RESULTADOS DE APRENDIZAJE
                    Al finalizar la asignatura, el estudiante será capaz de aplicar los conceptos de {a.Nombre}
                    para resolver problemas propios de su carrera.
                    """,
                ResultadosAprendizaje =
                    $"Al finalizar la asignatura, el estudiante será capaz de aplicar los conceptos de {a.Nombre}.",
                FechaCarga = DateTime.UtcNow
            }).ToList();

            await context.PlanesAnaliticos.AddRangeAsync(planes);
            await context.SaveChangesAsync();
        }

        /// <summary>Equivalencias de demostración entre carreras del catálogo (HU-14).</summary>
        private static async Task SembrarEquivalenciasAsync(SolicitudesDbContext context)
        {
            if (await context.Equivalencias.AnyAsync())
                return;

            var carreras = await context.Carreras.AsNoTracking().Select(c => c.Id).ToListAsync();
            if (carreras.Count < 2)
                return;

            var asignaturas = await context.Asignaturas.AsNoTracking().ToListAsync();

            // Pares de asignaturas con el mismo nombre en carreras distintas (origen -> destino)
            var pares = new[]
            {
                (Origen: 1, Destino: 46),   // Física (Software) -> Física (TI)
                (Origen: 2, Destino: 43),   // Álgebra Lineal (Software) -> Álgebra Lineal (TI)
                (Origen: 3, Destino: 45),   // Cálculo Diferencial (Software) -> Cálculo Diferencial (TI)
                (Origen: 3, Destino: 144),  // Cálculo Diferencial (Software) -> Cálculo Diferencial (Industrial)
                (Origen: 4, Destino: 87),   // Algoritmos y Lógica -> Fundamentos de programación
            };

            var equivalencias = new List<Equivalencia>();
            foreach (var (origenId, destinoId) in pares)
            {
                var origen = asignaturas.FirstOrDefault(a => a.Id == origenId);
                var destino = asignaturas.FirstOrDefault(a => a.Id == destinoId);
                if (origen is null || destino is null) continue;
                if (equivalencias.Any(e => e.AsignaturaOrigenId == origenId && e.AsignaturaDestinoId == destinoId))
                    continue;

                equivalencias.Add(new Equivalencia
                {
                    CarreraOrigenId = origen.CarreraId,
                    AsignaturaOrigenId = origen.Id,
                    CarreraDestinoId = destino.CarreraId,
                    AsignaturaDestinoId = destino.Id,
                    Activa = true,
                    FechaCreacion = DateTime.UtcNow
                });
            }

            if (equivalencias.Count > 0)
            {
                await context.Equivalencias.AddRangeAsync(equivalencias);
                await context.SaveChangesAsync();
            }
        }
    }
}
