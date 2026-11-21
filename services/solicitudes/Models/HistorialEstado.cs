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

    /// <summary>Nota opcional del movimiento; por ejemplo, que el estudiante editó la solicitud.</summary>
    public string? Detalle { get; set; }
}