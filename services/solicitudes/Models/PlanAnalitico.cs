namespace HomologaUTA.Solicitudes.Models;

/// <summary>Plan analítico vigente de una asignatura (HU-13).</summary>
public class PlanAnalitico
{
    public int Id { get; set; }

    public int AsignaturaId { get; set; }
    public Asignatura Asignatura { get; set; } = null!;

    public string Contenido { get; set; } = string.Empty;
    public string ResultadosAprendizaje { get; set; } = string.Empty;
    public DateTime FechaCarga { get; set; } = DateTime.UtcNow;
}
