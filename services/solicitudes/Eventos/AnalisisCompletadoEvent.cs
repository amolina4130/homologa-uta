namespace HomologaUTA.Solicitudes.Eventos;

public class AnalisisCompletadoEvent
{
    public int SolicitudId { get; set; }
    public string EstadoFinal { get; set; } = string.Empty; // Ej: PENDIENTE_REVISION
    public DateTime FechaAnalisis { get; set; } = DateTime.UtcNow;
}