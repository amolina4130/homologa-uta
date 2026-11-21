namespace HomologaUTA.Homologacion.Services;

public interface IInformeService
{
    // Devuelve los bytes del PDF
    Task<byte[]> GenerarPdfAsync(int solicitudId);
}