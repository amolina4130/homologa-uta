using UglyToad.PdfPig;

namespace HomologaUTA.Documentos.Services;

public class ExtractorPdfService
{
    public virtual string ExtraerTexto(string rutaPdf)
    {
        if (!File.Exists(rutaPdf))
            throw new FileNotFoundException("El archivo PDF no existe.");

        var texto = string.Empty;

        using (var document = PdfDocument.Open(rutaPdf))
        {
            foreach (var page in document.GetPages())
            {
                texto += page.Text + "\n";
            }
        }

        return texto;
    }

    /// <summary>
    /// Texto conservando la disposición de columnas (pdftotext -layout). Los planes analíticos y
    /// sílabos son tablas: sin columnas no se distingue el tema de las horas o del resultado.
    /// Si pdftotext no está disponible se usa la extracción normal.
    /// </summary>
    public virtual string ExtraerTextoConDiseno(string rutaPdf)
    {
        try
        {
            var inicio = new System.Diagnostics.ProcessStartInfo("pdftotext")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
            };
            foreach (var arg in new[] { "-enc", "UTF-8", "-layout", rutaPdf, "-" })
                inicio.ArgumentList.Add(arg);

            using var proceso = System.Diagnostics.Process.Start(inicio)!;
            var salida = proceso.StandardOutput.ReadToEnd();
            proceso.WaitForExit(30000);
            if (proceso.ExitCode == 0 && !string.IsNullOrWhiteSpace(salida))
                return salida;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // pdftotext no instalado: se usa PdfPig.
        }

        return ExtraerTexto(rutaPdf);
    }
}