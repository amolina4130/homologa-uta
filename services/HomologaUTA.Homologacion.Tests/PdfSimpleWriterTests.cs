using System.Text;
using HomologaUTA.Homologacion.Reportes;

namespace HomologaUTA.Homologacion.Tests;

/// <summary>Pruebas del generador de PDF sin dependencias (HU-18).</summary>
public class PdfSimpleWriterTests
{
    [Fact]
    public void Crear_GeneraUnPdfValidoConTituloYPie()
    {
        var bytes = PdfSimpleWriter.Crear(
            "Informe de Homologacion",
            "Sistema de Homologacion UTA",
            new[] { "Solicitud SOL-0001", "Estudiante: estudiante@uta.edu.ec" });

        var texto = Encoding.ASCII.GetString(bytes);

        Assert.StartsWith("%PDF-1.4", texto);
        Assert.Contains("%%EOF", texto);
        Assert.Contains("Informe de Homologacion", texto);
        Assert.Contains("Sistema de Homologacion UTA - Pagina 1 de 1", texto);
        Assert.Contains("Solicitud SOL-0001", texto);
    }

    [Fact]
    public void Crear_ConMuchasLineas_GeneraVariasPaginas()
    {
        var lineas = Enumerable.Range(1, 120)
            .Select(i => $"Linea de prueba numero {i} con contenido suficiente para el informe.");

        var bytes = PdfSimpleWriter.Crear("Informe largo", "Pie", lineas);
        var texto = Encoding.ASCII.GetString(bytes);

        var paginas = ContarPaginas(texto);
        Assert.True(paginas >= 3, $"Se esperaban al menos 3 paginas y se generaron {paginas}.");
        Assert.Contains($"Pagina {paginas} de {paginas}", texto);
    }

    [Fact]
    public void Crear_EscapaParentesisYBarraInvertida()
    {
        var bytes = PdfSimpleWriter.Crear("Titulo (con parentesis)", "Pie", new[] { @"Ruta C:\temp\archivo" });
        var texto = Encoding.ASCII.GetString(bytes);

        Assert.Contains(@"\(con parentesis\)", texto);
        Assert.Contains(@"C:\\temp\\archivo", texto);
    }

    [Fact]
    public void Crear_SinLineas_GeneraAlMenosUnaPagina()
    {
        var bytes = PdfSimpleWriter.Crear("Vacio", "Pie", Array.Empty<string>());
        var texto = Encoding.ASCII.GetString(bytes);

        Assert.Contains("/Count 1", texto);
        Assert.Contains("Pagina 1 de 1", texto);
    }

    private static int ContarPaginas(string texto) =>
        texto.Split("/Type /Page /Parent").Length - 1;
}
