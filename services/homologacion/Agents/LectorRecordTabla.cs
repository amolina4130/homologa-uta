using System.Globalization;
using System.Text.RegularExpressions;

namespace HomologaUTA.Homologacion.Agents;

/// <summary>
/// Lee el récord académico cuando viene en el formato de tabla de la UTA:
/// código (8 dígitos) · asignatura · créditos · nota · asistencia % · estado, agrupadas por
/// "Nivel N — periodo". Es inmediato y no inventa nada: solo toma lo que está en el texto.
/// Si el documento tiene otro formato devuelve una lista vacía y se usa el agente de IA.
/// </summary>
public static class LectorRecordTabla
{
    /// <summary>Mínimo de filas reconocidas para confiar en la lectura por formato.</summary>
    public const int MinimoMaterias = 3;

    private const string Estados = @"Aprobad[ao]|Reprobad[ao]|Retirad[ao]|Anulad[ao]|Homologad[ao]|Convalidad[ao]|Perdid[ao]|Pérdid[ao]";

    // El texto extraído del PDF suele venir sin espacios entre columnas ("08060112Álgebra Lineal38.593Aprobada"),
    // por eso cada columna se reconoce por su forma: créditos 1-2 dígitos, nota con decimales, asistencia 2-3 dígitos.
    private static readonly Regex Fila = new(
        @"(?<codigo>\d{8})\s*(?<nombre>[^\d\n]{3,120}?)\s*(?<creditos>\d{1,2})\s*(?<nota>(?:10|\d)[.,]\d\d?)\s*(?<asistencia>100|\d{2})?\s*%?\s*(?<estado>" + Estados + @")",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Nivel = new(
        @"Nivel\s*(?<n>\d{1,2})\s*[—–-]+\s*(?<periodo>[A-Za-zÁÉÍÓÚáéíóú]{3,10}\.?\s*\d{4}\s*[—–-]+\s*[A-Za-zÁÉÍÓÚáéíóú]{3,10}\.?\s*\d{4})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Estudiante = new(
        @"Estudiante\s*:?\s*(?<nombre>[A-Za-zÁÉÍÓÚÑáéíóúñ ]{5,120}?)\s*C[ée]dula",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Cedula = new(
        @"C[ée]dula[^\d]{0,30}(?<cedula>\d{10})(?!\d)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static ResultadoRecordAgente Leer(string? texto)
    {
        var resultado = new ResultadoRecordAgente();
        if (string.IsNullOrWhiteSpace(texto)) return resultado;

        var niveles = Nivel.Matches(texto)
            .Select(m => (Posicion: m.Index, Periodo: Regex.Replace(m.Groups["periodo"].Value, @"\s+", " ").Trim()))
            .ToList();

        foreach (Match m in Fila.Matches(texto))
        {
            var nombre = LimpiarNombre(m.Groups["nombre"].Value);
            if (nombre.Length < 3) continue;

            decimal? nota = decimal.TryParse(m.Groups["nota"].Value.Replace(',', '.'),
                NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null;

            resultado.Materias.Add(new MateriaRecordAgente
            {
                Nombre = nombre,
                Nota = nota,
                EstadoSegunDocumento = m.Groups["estado"].Value.Trim().ToLowerInvariant(),
                Periodo = niveles.LastOrDefault(x => x.Posicion < m.Index).Periodo,
            });
        }

        var est = Estudiante.Match(texto);
        if (est.Success) resultado.Nombre = Regex.Replace(est.Groups["nombre"].Value, @"\s+", " ").Trim();
        var ced = Cedula.Match(texto);
        if (ced.Success) resultado.Cedula = ced.Groups["cedula"].Value;

        return resultado;
    }

    private static string LimpiarNombre(string nombre)
    {
        // Saltos de línea dentro de la celda pegan palabras ("Comunicación deDatos").
        var limpio = Regex.Replace(nombre, @"(?<=\p{Ll})(?=\p{Lu})", " ");
        return Regex.Replace(limpio, @"\s+", " ").Trim(' ', '-', '·', '|');
    }
}
