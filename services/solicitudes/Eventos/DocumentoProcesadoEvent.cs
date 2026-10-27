namespace HomologaUTA.Solicitudes.Eventos;

public class DocumentoProcesadoEvent
{
    public int DocumentoId { get; set; }
    public int SolicitudId { get; set; }
    public string TextoExtraido { get; set; } = string.Empty;
    public DateTime FechaProcesamiento { get; set; } = DateTime.UtcNow;
}