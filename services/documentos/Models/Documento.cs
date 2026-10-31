namespace HomologaUTA.Documentos.Models;

public class Documento
{
    public int Id { get; set; }
    public int SolicitudId { get; set; }
    public string TipoDocumento { get; set; } = string.Empty; // malla, silabo, historial
    public string NombreArchivo { get; set; } = string.Empty;
    public string TextoExtraido { get; set; } = string.Empty;
    public string Estado { get; set; } = "PENDIENTE"; // PENDIENTE o PROCESADO
    public DateTime FechaSubida { get; set; } = DateTime.UtcNow;
}