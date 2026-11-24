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
