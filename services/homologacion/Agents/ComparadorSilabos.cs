using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace HomologaUTA.Homologacion.Agents;

/// <summary>Resultado de comparar el sílabo de origen con el de destino.</summary>
public class ComparacionSilabos
{
    /// <summary>% de los contenidos del destino que aparecen en el origen.</summary>
    public int Contenido { get; set; }

    /// <summary>% de los resultados de aprendizaje del destino cubiertos por el origen (Art. 20).</summary>
    public int Profundidad { get; set; }

    /// <summary>Horas de origen respecto a las de destino (tope 100 %). null si falta el dato.</summary>
    public int? Horas { get; set; }

    public List<string> Coincidencias { get; set; } = new();
    public List<string> Diferencias { get; set; } = new();

    /// <summary>Qué datos faltaron para la comparación; nunca se inventan.</summary>
    public List<string> DatosFaltantes { get; set; } = new();
}

/// <summary>
/// Método de cálculo definido para la homologación por análisis comparativo de contenidos
/// (R-CAU-P-011-2026 y R-CAU-P-122-2026, Art. 20): el contenido, la profundidad y la carga
/// horaria del origen deben ser al menos el 80 % de los de la asignatura receptora (destino).
///
/// - Contenido: el sílabo de destino se divide en temas; un tema cuenta como cubierto cuando al
///   menos la mitad de sus términos significativos aparece en el sílabo de origen.
/// - Profundidad: el mismo cálculo sobre los resultados de aprendizaje del destino.
/// - Horas: horas de origen / horas de destino según las mallas.
/// Es determinista: la misma entrada da siempre el mismo porcentaje.
/// </summary>
public static class ComparadorSilabos
{
    private const double UmbralTema = 0.5;
    private const int MaxTemasListados = 8;

    private static readonly HashSet<string> Vacias = new(StringComparer.Ordinal)
    {
        "para", "por", "con", "los", "las", "del", "una", "uno", "que", "como", "sus", "entre", "sobre",
        "mediante", "desde", "hasta", "este", "esta", "estos", "estas", "aplicaciones", "aplicacion",
        "introduccion", "conceptos", "concepto", "basicos", "basicas", "fundamentos", "generales", "general",
        "tipos", "caso", "casos", "tema", "temas", "unidad", "objetivo", "objetivos", "estudiante",
        "resolucion", "problemas", "asignatura", "aprendizaje", "permitan", "permita", "traves", "manera",
        "forma", "diferentes", "principales", "respectivas", "respectivos", "acuerdo"
    };

    public static ComparacionSilabos Comparar(
        string? contenidoOrigen, string? resultadosOrigen, int? horasOrigen,
        string? contenidoDestino, string? resultadosDestino, int? horasDestino)
    {
        var r = new ComparacionSilabos();
        // Un plan genérico de relleno ("Unidades: fundamentos, conceptos esenciales…") no describe la
        // asignatura: compararlo daría coincidencias falsas. Se trata como dato faltante.
        if (EsPlanGenerico(contenidoDestino)) contenidoDestino = null;
        if (EsPlanGenerico(contenidoOrigen)) contenidoOrigen = null;
        var bolsaOrigen = Tokens($"{contenidoOrigen}\n{resultadosOrigen}").ToHashSet(StringComparer.Ordinal);

        var temas = Temas(contenidoDestino);
        if (temas.Count == 0)
        {
            r.DatosFaltantes.Add("contenidos de la asignatura de destino");
        }
        else
        {
            var cubiertos = 0;
            foreach (var tema in temas)
            {
                if (Cubierto(tema, bolsaOrigen))
                {
                    cubiertos++;
                    r.Coincidencias.Add(tema);
                }
                else
                {
                    r.Diferencias.Add(tema);
                }
            }
            r.Contenido = (int)Math.Round(100.0 * cubiertos / temas.Count);
        }

        if (string.IsNullOrWhiteSpace(contenidoOrigen))
            r.DatosFaltantes.Add("sílabo de la asignatura de origen");

        var resultados = Temas(resultadosDestino);
        if (resultados.Count == 0)
        {
            // Sin resultados de aprendizaje registrados, la profundidad se estima con el contenido.
            r.Profundidad = r.Contenido;
            r.DatosFaltantes.Add("resultados de aprendizaje del destino (la profundidad se estimó con el contenido)");
        }
        else
        {
            var cubiertos = resultados.Count(t => Cubierto(t, bolsaOrigen));
            r.Profundidad = (int)Math.Round(100.0 * cubiertos / resultados.Count);
        }

        if (horasOrigen is > 0 && horasDestino is > 0)
            r.Horas = Math.Min(100, (int)Math.Round(100.0 * horasOrigen.Value / horasDestino.Value));
        else
            r.DatosFaltantes.Add("horas de una de las asignaturas");

        r.Coincidencias = r.Coincidencias.Take(MaxTemasListados).ToList();
        r.Diferencias = r.Diferencias.Take(MaxTemasListados).ToList();
        return r;
    }

    /// <summary>true si el texto es el plan de relleno que se crea cuando no hay plan analítico real.</summary>
    public static bool EsPlanGenerico(string? texto) =>
        !string.IsNullOrWhiteSpace(texto) &&
        Regex.IsMatch(texto, @"Unidades:\s*fundamentos,\s*conceptos esenciales", RegexOptions.IgnoreCase);

    /// <summary>Total de horas del semestre que declara un sílabo o plan analítico (formato UTA); null si no está.</summary>
    public static int? HorasDeSilabo(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var m = Regex.Match(texto, @"TOTAL\s*DE\s*HORAS\s*AL\s*SEMESTRE\D{0,300}?(\d{2,3})", RegexOptions.IgnoreCase);
        return m.Success && int.TryParse(m.Groups[1].Value, out var h) && h > 0 ? h : null;
    }

    private static bool Cubierto(string tema, HashSet<string> bolsa)
    {
        var t = Tokens(tema).Distinct(StringComparer.Ordinal).ToList();
        return t.Count > 0 && (double)t.Count(bolsa.Contains) / t.Count >= UmbralTema;
    }

    /// <summary>Divide un sílabo en temas: por unidad, punto y coma y punto.</summary>
    internal static List<string> Temas(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return new List<string>();

        return texto.Split('\n')
            .Where(l => !l.TrimStart().StartsWith("Objetivo general", StringComparison.OrdinalIgnoreCase))
            .SelectMany(l => Regex.Replace(l, @"^\s*U\.\d+\s*", "").Replace("Temas:", ";").Split(';', '.', '*', '•'))
            .Select(s => s.Trim().TrimStart('*', '-', '•', ' ').Trim())
            .Where(s => s.Length >= 4 && Tokens(s).Any())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Términos significativos: sin tildes, sin palabras vacías y recortados a su raíz (6 letras).</summary>
    internal static IEnumerable<string> Tokens(string? texto)
    {
        var limpio = SinTildes(texto ?? string.Empty);
        foreach (var palabra in Regex.Split(limpio, "[^a-z0-9]+"))
        {
            if (palabra.Length <= 3 || Vacias.Contains(palabra)) continue;
            yield return palabra.Length > 6 ? palabra[..6] : palabra;
        }
    }

    private static string SinTildes(string texto)
    {
        var d = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().ToLowerInvariant();
    }
}
