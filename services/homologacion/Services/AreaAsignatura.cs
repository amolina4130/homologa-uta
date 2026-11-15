using System.Globalization;
using System.Text;

namespace HomologaUTA.Homologacion.Services;

/// <summary>
/// Clasificación de asignaturas por área y conversión del nivel textual a número (HU-19, HU-21).
/// Se mantiene igual que la del Catálogo para que la preselección sea coherente.
/// </summary>
public static class AreaAsignatura
{
    private static readonly (string Area, string[] Claves)[] Tabla =
    {
        ("Matemáticas", new[] { "calculo", "álgebra", "algebra", "matematic", "estadístic", "estadistic", "probabilidad", "geometrí", "geometri", "ecuaciones diferenciales" }),
        ("Física", new[] { "física", "fisica", "mecánica", "mecanica", "electromagnet", "óptica", "optica", "termodinám", "termodinam", "ondas" }),
        ("Química", new[] { "química", "quimica", "laboratorio químic", "laboratorio quimic" }),
        ("Programación", new[] { "programaci", "algoritm", "software", "base de datos", "estructura de datos", "sistemas operativos", "computaci", "lógica matemática", "logica matematica" }),
        ("Electrónica", new[] { "electrónic", "electronic", "circuit", "control", "automatiz", "robot", "telecomunicacion", "telecomunicaci", "señales", "senales", "redes" }),
        ("Industrial", new[] { "producci", "manufactur", "logístic", "logistic", "procesos industriales", "calidad" }),
        ("Administración", new[] { "administraci", "gestión", "gestion", "econom", "contabil", "marketing", "empresa", "finanz" }),
        ("Humanística", new[] { "metodolog", "investigaci", "comunicaci", "ética", "etica", "sociedad", "lenguaje", "inglés", "ingles", "humanístic", "humanistic" }),
    };

    public static string Detectar(string nombre)
    {
        var texto = Normalizar(nombre);
        foreach (var (area, claves) in Tabla)
        {
            if (claves.Any(c => texto.Contains(Normalizar(c), StringComparison.Ordinal)))
                return area;
        }
        return "General";
    }

    public static int NivelNumerico(string? nivel)
    {
        if (string.IsNullOrWhiteSpace(nivel)) return 1;
        var texto = Normalizar(nivel);

        var nombres = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["primero"] = 1, ["segundo"] = 2, ["tercero"] = 3, ["cuarto"] = 4, ["quinto"] = 5,
            ["sexto"] = 6, ["septimo"] = 7, ["octavo"] = 8, ["noveno"] = 9, ["decimo"] = 10
        };
        foreach (var par in nombres)
            if (texto.Contains(par.Key)) return par.Value;

        var digitos = new string(texto.Where(char.IsDigit).ToArray());
        return int.TryParse(digitos, out var n) && n > 0 ? n : 1;
    }

    public static string Normalizar(string texto)
    {
        var sinAcentos = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(sinAcentos.Length);
        foreach (var c in sinAcentos)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
