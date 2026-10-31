using UglyToad.PdfPig;

namespace HomologaUTA.Documentos.Services;

public class ExtractorPdfService
{
    public string ExtraerTexto(string rutaPdf)
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
}