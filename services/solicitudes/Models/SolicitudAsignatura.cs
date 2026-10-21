namespace HomologaUTA.Solicitudes.Models;

public class SolicitudAsignatura
{
    public int Id { get; set; }
    public int SolicitudId { get; set; }
    public Solicitud Solicitud { get; set; } = null!;
    
    public int AsignaturaId { get; set; }
    public Asignatura Asignatura { get; set; } = null!;
}