   namespace HomologaUTA.Solicitudes.Models;

public class HistorialEstado
{
    public int Id { get; set; }

    public int SolicitudId { get; set; }
    public Solicitud Solicitud { get; set; } = null!;

    public string? EstadoAnterior { get; set; }
    public string EstadoNuevo { get; set; } = string.Empty;

    public string UsuarioCorreo { get; set; } = string.Empty;
    public DateTime FechaCambio { get; set; } = DateTime.UtcNow;
}