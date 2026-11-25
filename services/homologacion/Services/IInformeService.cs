namespace HomologaUTA.Homologacion.Services;

public interface IInformeService
{
    /// <summary>Devuelve los bytes del PDF de la solicitud.</summary>
    Task<byte[]> GenerarPdfAsync(int solicitudId, CancellationToken ct = default);
}