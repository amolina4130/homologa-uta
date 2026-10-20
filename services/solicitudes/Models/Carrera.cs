namespace HomologaUTA.Solicitudes.Models;

public class Carrera
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public List<Asignatura> Asignaturas { get; set; } = new();
}