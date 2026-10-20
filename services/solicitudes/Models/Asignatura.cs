namespace HomologaUTA.Solicitudes.Models;

public class Asignatura
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Nivel { get; set; } = string.Empty;
    public int Creditos { get; set; }
    public int Horas { get; set; }
    public int CarreraId { get; set; }
    public Carrera Carrera { get; set; } = null!;
}