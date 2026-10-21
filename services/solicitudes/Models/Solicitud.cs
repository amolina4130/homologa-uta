namespace HomologaUTA.Solicitudes.Models;

public class Solicitud
{
    public int Id { get; set; }
    public string Tipo { get; set; } = string.Empty; 
    
    public int CarreraOrigenId { get; set; }
    public Carrera CarreraOrigen { get; set; } = null!;
    
    public int CarreraDestinoId { get; set; }
    public Carrera CarreraDestino { get; set; } = null!;
    
    public string EstudianteCorreo { get; set; } = string.Empty; 
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public string Estado { get; set; } = EstadoSolicitud.REGISTRADA;
    
    public List<SolicitudAsignatura> Asignaturas { get; set; } = new();
}