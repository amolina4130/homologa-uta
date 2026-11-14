namespace HomologaUTA.Solicitudes.Services;

/// <summary>
/// Clasifica una asignatura en un área por palabras clave. Se usa para la memoria del
/// agente (HU-19) y para la preselección de candidatas (HU-21).
/// </summary>
public static class AreaAsignatura
{
    private static readonly (string Area, string[] Palabras)[] Reglas =
    {
        ("Matemáticas", new[] { "algebra", "cálculo", "calculo", "matemátic", "matematic", "estadístic", "estadistic", "numéric", "numeric", "lógica matemática", "logica matematica" }),
        ("Física", new[] { "física", "fisica", "mecánica", "mecanica", "eléctric", "electric", "óptic", "optic", "termodinám", "termodinam" }),
        ("Programación", new[] { "programaci", "algoritmo", "software", "datos", "base de datos", "computaci", "aplicaciones", "web", "móvil", "movil", "sistemas operativos", "redes" }),
        ("Química", new[] { "química", "quimica", "laboratorio químico" }),
        ("Biología", new[] { "biolog", "biológic" }),
        ("Electrónica", new[] { "electrónic", "electronic", "circuit", "control", "automatiz", "robótica", "robotica", "telecomunicacion", "señal", "senal" }),
        ("Industrial", new[] { "industrial", "producción", "produccion", "calidad", "gestión de proyectos", "gestion de proyectos", "económic", "economic", "emprendimiento" }),
        ("Humanidades", new[] { "realidad nacional", "metodología", "metodologia", "investigaci", "legislaci", "comunicaci", "lenguaje", "inglés", "ingles" })
    };

    public static string Detectar(string nombre)
    {
        var texto = (nombre ?? string.Empty).ToLowerInvariant();
        foreach (var (area, palabras) in Reglas)
        {
            if (palabras.Any(p => texto.Contains(p, StringComparison.OrdinalIgnoreCase)))
                return area;
        }
        return "General";
    }

    public static int NivelNumerico(string nivel)
    {
        var texto = (nivel ?? string.Empty).ToLowerInvariant();
        var mapa = new Dictionary<string, int>
        {
            ["primero"] = 1, ["segundo"] = 2, ["tercero"] = 3, ["cuarto"] = 4,
            ["quinto"] = 5, ["sexto"] = 6, ["séptimo"] = 7, ["septimo"] = 7,
            ["octavo"] = 8, ["noveno"] = 9, ["décimo"] = 10, ["decimo"] = 10
        };
        foreach (var par in mapa)
        {
            if (texto.Contains(par.Key, StringComparison.OrdinalIgnoreCase))
                return par.Value;
        }
        return int.TryParse(texto, out var n) ? n : 0;
    }
}
