using System.Globalization;

namespace HomologaUTA.Homologacion.Reportes;

/// <summary>
/// Escritor de PDF mínimo sin dependencias nativas (HU-18). Genera páginas A4 con texto
/// Helvetica de 11 pt, saltos de línea automáticos y pie de página.
/// </summary>
public static class PdfSimpleWriter
{
    private const int AnchoA4 = 595;
    private const int AltoA4 = 842;
    private const int Margen = 50;
    private const int CaracteresPorLinea = 92;
    private const int LineasPorPagina = 46;

    public static byte[] Crear(string titulo, string piePagina, IEnumerable<string> lineas)
    {
        var contenido = new List<string>();
        foreach (var linea in lineas)
            contenido.AddRange(Partir(linea));

        var paginas = new List<List<string>>();
        for (var i = 0; i < contenido.Count; i += LineasPorPagina)
            paginas.Add(contenido.Skip(i).Take(LineasPorPagina).ToList());
        if (paginas.Count == 0) paginas.Add(new List<string>());

        var objetos = new List<string>();
        var totalPaginas = paginas.Count;

        // 1 catálogo, 2 pages, 3 font; páginas desde 4; contenidos desde 4 + totalPaginas
        objetos.Add("<< /Type /Catalog /Pages 2 0 R >>");
        var kids = string.Join(" ", Enumerable.Range(0, totalPaginas)
            .Select(i => $"{4 + i} 0 R"));
        objetos.Add($"<< /Type /Pages /Kids [{kids}] /Count {totalPaginas} >>");
        objetos.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");

        for (var i = 0; i < totalPaginas; i++)
        {
            var contenidoId = 4 + totalPaginas + i;
            objetos.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {AnchoA4} {AltoA4}] " +
                        $"/Resources << /Font << /F1 3 0 R >> >> /Contents {contenidoId} 0 R >>");
        }

        for (var i = 0; i < totalPaginas; i++)
        {
            var lineasPagina = paginas[i];
            var texto = new System.Text.StringBuilder();
            texto.AppendLine("BT");
            texto.AppendLine("/F1 11 Tf");
            texto.AppendLine("14 TL");
            texto.AppendLine($"{Margen} {AltoA4 - Margen} Td");

            // Título de la primera página
            if (i == 0)
            {
                texto.AppendLine("/F1 14 Tf");
                texto.AppendLine($"({Escapar(SinAcentos(titulo))}) Tj");
                texto.AppendLine("T*");
                texto.AppendLine("/F1 11 Tf");
                texto.AppendLine("T*");
            }

            foreach (var linea in lineasPagina)
            {
                texto.AppendLine($"({Escapar(linea)}) Tj");
                texto.AppendLine("T*");
            }

            texto.AppendLine($"({Escapar(SinAcentos($"{piePagina} - Página {i + 1} de {totalPaginas}"))}) Tj");
            texto.AppendLine("ET");

            var stream = texto.ToString();
            objetos.Add($"<< /Length {System.Text.Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}\nendstream");
        }

        return Escribir(objetos);
    }

    private static byte[] Escribir(List<string> objetos)
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, System.Text.Encoding.ASCII, leaveOpen: true);
        writer.NewLine = "\n";

        writer.WriteLine("%PDF-1.4");
        var offsets = new List<long> { 0 };

        for (var i = 0; i < objetos.Count; i++)
        {
            writer.Flush();
            offsets.Add(ms.Position);
            writer.WriteLine($"{i + 1} 0 obj");
            writer.WriteLine(objetos[i]);
            writer.WriteLine("endobj");
        }

        writer.Flush();
        var inicioXref = ms.Position;
        writer.WriteLine("xref");
        writer.WriteLine($"0 {objetos.Count + 1}");
        writer.WriteLine("0000000000 65535 f ");
        for (var i = 1; i <= objetos.Count; i++)
            writer.WriteLine($"{offsets[i]:D10} 00000 n ");

        writer.WriteLine("trailer");
        writer.WriteLine($"<< /Size {objetos.Count + 1} /Root 1 0 R >>");
        writer.WriteLine("startxref");
        writer.WriteLine(inicioXref.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine("%%EOF");
        writer.Flush();

        return ms.ToArray();
    }

    private static IEnumerable<string> Partir(string linea)
    {
        var limpia = SinAcentos(linea ?? string.Empty).Replace("\r", "");
        if (limpia.Length <= CaracteresPorLinea)
        {
            yield return limpia;
            yield break;
        }

        var palabras = limpia.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var actual = new System.Text.StringBuilder();
        foreach (var palabra in palabras)
        {
            if (actual.Length + palabra.Length + 1 > CaracteresPorLinea && actual.Length > 0)
            {
                yield return actual.ToString();
                actual.Clear();
            }
            if (actual.Length > 0) actual.Append(' ');
            actual.Append(palabra);
        }
        if (actual.Length > 0) yield return actual.ToString();
    }

    private static string SinAcentos(string texto)
    {
        var normalizado = texto.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(normalizado.Length);
        foreach (var c in normalizado)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }

    private static string Escapar(string texto) =>
        texto.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
