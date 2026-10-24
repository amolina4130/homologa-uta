using HomologaUTA.Reglas.Models;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Reglas.Data;

/// <summary>Datos semilla del catálogo RN-01 a RN-11 (documento de planificación, sección 5.3).</summary>
public static class ReglaSeed
{
    public static readonly IReadOnlyList<Regla> Reglas = new List<Regla>
    {
        new() { Codigo = "RN-01", Umbral = 10m, Fuente = "Instructivo, lit. B y C",
            Descripcion = "Solo procede homologar si no han pasado más de diez años desde la aprobación; si pasó más tiempo, se deriva a validación de conocimientos." },
        new() { Codigo = "RN-02", Fuente = "Instructivo, lit. B",
            Descripcion = "Únicamente se homologan asignaturas aprobadas." },
        new() { Codigo = "RN-03", Umbral = 80m, Fuente = "Instructivo lit. B y C; CAU-P-122 art. 20",
            Descripcion = "La asignatura de origen debe alcanzar al menos el 80 % de la receptora en contenido, profundidad y carga horaria (en movilidad, 80 % en resultados de aprendizaje)." },
        new() { Codigo = "RN-04", Umbral = 3m, Fuente = "Instructivo, lit. H",
            Descripcion = "Se excluyen las asignaturas que llegaron a tercera matrícula." },
        new() { Codigo = "RN-05", Umbral = 2m, Fuente = "Instructivo, lit. E",
            Descripcion = "Cambio de universidad: sin cambios de carrera previos, con al menos dos períodos cursados y dos asignaturas aprobadas homologables." },
        new() { Codigo = "RN-06", Fuente = "Instructivo D y E; CAU-P-122 art. 13",
            Descripcion = "La solicitud debe traer toda la documentación exigida; en universidades extranjeras, documentos apostillados." },
        new() { Codigo = "RN-07", Fuente = "CAU-P-122 art. 19",
            Descripcion = "Las notas de movilidad se convierten con la tabla oficial de equivalencias." },
        new() { Codigo = "RN-08", Fuente = "CAU-P-122 art. 11 y 17",
            Descripcion = "Las asignaturas sin equivalente se marcan para tutorías de continuidad académica." },
        new() { Codigo = "RN-09", Umbral = 5m, Fuente = "Instructivo, lit. C.1",
            Descripcion = "El Coordinador dispone de un máximo de cinco días término para su informe técnico." },
        new() { Codigo = "RN-10", Fuente = "CAU-P-122 art. 8 y disp. gral. segunda",
            Descripcion = "El agente solo propone; decide el Consejo Directivo de Facultad." },
        new() { Codigo = "RN-11", Fuente = "Instructivo, lit. B",
            Descripcion = "La carrera receptora verifica que lo homologado no comprometa el perfil de egreso." },
    };

    /// <summary>
    /// Inserta solo las reglas cuyo código aún no existe. Es idempotente: repetir la carga
    /// no duplica registros ni pisa los umbrales o estados que el Administrador haya modificado.
    /// </summary>
    public static async Task InitializeAsync(ReglasDbContext context)
    {
        var existentes = await context.Reglas.Select(r => r.Codigo).ToListAsync();

        var nuevas = Reglas
            .Where(r => !existentes.Contains(r.Codigo))
            .Select(r => new Regla
            {
                Codigo = r.Codigo,
                Descripcion = r.Descripcion,
                Fuente = r.Fuente,
                Umbral = r.Umbral,
                Activa = true
            })
            .ToList();

        if (nuevas.Count == 0) return;

        await context.Reglas.AddRangeAsync(nuevas);
        await context.SaveChangesAsync();
    }
}
