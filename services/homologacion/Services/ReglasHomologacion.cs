using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace HomologaUTA.Homologacion.Services;

/// <summary>
/// Reglas institucionales que se aplican ANTES de cualquier porcentaje o sugerencia de IA.
/// Estas materias tienen contenidos propios de la carrera destino (trabajo de titulación,
/// prácticas presenciales o proyecto final), por lo que nunca se homologan aunque el
/// contenido, el área o el modelo digan lo contrario.
/// </summary>
public static class ReglasHomologacion
{
    public const string MotivoMateriaNoHomologable =
        "No se homologa por regla institucional: Desarrollo de Proyectos, Titulación y " +
        "Prácticas Laborales corresponden a actividades finales o de formación práctica " +
        "de la carrera destino, con contenidos distintos a una asignatura regular.";

    private static readonly Regex MateriaExcluida = new(
        @"\b(desarrollo\s+de\s+proyectos|titulaci[oó]n|pr[aá]ctica(s)?\s+laboral(es)?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>True si alguno de los nombres corresponde a una materia no homologable.</summary>
    public static bool EsMateriaNoHomologable(params string?[] nombres) =>
        nombres.Any(n => !string.IsNullOrWhiteSpace(n) && MateriaExcluida.IsMatch(SinAcentos(n)));

    /// <summary>Devuelve el motivo institucional cuando la materia está excluida; si no, null.</summary>
    public static string? MotivoSiNoHomologable(params string?[] nombres) =>
        EsMateriaNoHomologable(nombres) ? MotivoMateriaNoHomologable : null;

    /// <summary>
    /// true si el récord indica que la materia está aprobada. Sin estado se asume aprobada (la
    /// extracción ya filtra por aprobadas); "reprobado", "retirado", "anulado", "pérdida" no.
    /// </summary>
    public static bool EstaAprobada(string? estadoSegunDocumento)
    {
        if (string.IsNullOrWhiteSpace(estadoSegunDocumento)) return true;
        var e = SinAcentos(estadoSegunDocumento);
        return !Regex.IsMatch(e, @"reprob|no\s*aprob|retir|anulad|perdid|perdi[oó]|desaprob|suspens");
    }

    /// <summary>Años máximos desde la aprobación para homologar por análisis de contenidos (R-CAU-P-011-2026).</summary>
    public const int AniosMaximosAprobacion = 10;

    /// <summary>
    /// Reglas que dependen del récord (R-CAU-P-011-2026): la homologación por análisis de
    /// contenidos solo procede hasta 10 años después de aprobar la materia, y no se homologan
    /// materias objeto de tercera matrícula. Si el récord no trae el dato, no se aplica (no se
    /// inventa) y el docente lo verifica.
    /// </summary>
    public static string? MotivoPorRecord(string? periodo, string? estadoSegunRecord, int? anioActual = null)
    {
        if (!string.IsNullOrWhiteSpace(estadoSegunRecord) &&
            Regex.IsMatch(SinAcentos(estadoSegunRecord), @"\b(tercera|3ra|3a)\b"))
        {
            return "No se homologa: la materia fue objeto de tercera matrícula (R-CAU-P-011-2026).";
        }

        if (string.IsNullOrWhiteSpace(periodo)) return null;

        var anios = Regex.Matches(periodo, @"\b(19|20)\d{2}\b").Select(m => int.Parse(m.Value)).ToList();
        if (anios.Count == 0) return null;

        var aprobada = anios.Max();
        var actual = anioActual ?? DateTime.UtcNow.Year;
        return actual - aprobada > AniosMaximosAprobacion
            ? $"No procede la homologación por análisis de contenidos: la materia se aprobó en {aprobada}, hace más de " +
              $"{AniosMaximosAprobacion} años; corresponde validación de conocimientos (R-CAU-P-011-2026)."
            : null;
    }

    /// <summary>Nombre visible de una fila, incluyendo las materias origen combinadas.</summary>
    public static string NombreOrigen(string asignaturaOrigen, bool combinada, IEnumerable<string>? materiasOrigen)
    {
        var lista = materiasOrigen?.Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m.Trim()).ToList()
                    ?? new List<string>();
        return combinada && lista.Count > 0 ? string.Join(" + ", lista) : asignaturaOrigen;
    }

    private static string SinAcentos(string texto)
    {
        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var resultado = new StringBuilder(descompuesto.Length);
        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
                resultado.Append(caracter);
        }
        return resultado.ToString().ToLowerInvariant();
    }
}
