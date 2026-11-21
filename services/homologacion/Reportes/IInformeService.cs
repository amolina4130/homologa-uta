namespace HomologaUTA.Homologacion.Reportes;

/// <summary>Genera el informe de homologación de una solicitud en PDF.</summary>
public interface IInformeService
{
    /// <summary>
    /// Devuelve los bytes del PDF del informe de la solicitud.
    /// Lanza AnalisisSolicitudException (404) si la solicitud no existe o todavía no tiene análisis,
    /// y SolicitudesNoDisponibleException si el servicio de Solicitudes no responde.
    /// </summary>
    Task<byte[]> GenerarPdf(int solicitudId, CancellationToken ct = default);
}
