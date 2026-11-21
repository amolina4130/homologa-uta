namespace HomologaUTA.Homologacion.Services;

public class InformeServiceSimulado : IInformeService
{
    public Task<byte[]> GenerarPdfAsync(int solicitudId)
    {
        // Simulamos un PDF con un texto plano (en la HU-18 real se usará QuestPDF)
        var contenido = $"INFORME DE HOMOLOGACIÓN - SOLICITUD {solicitudId}\n\nEste es un PDF de ejemplo generado para probar el endpoint.";
        var bytes = System.Text.Encoding.UTF8.GetBytes(contenido);
        
        return Task.FromResult(bytes);
    }
}