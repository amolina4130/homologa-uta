namespace HomologaUTA.Solicitudes.Models;

/// <summary>Plan analítico vigente de una asignatura (HU-13). Una asignatura tiene como máximo un plan.</summary>
public class PlanAnalitico
{
    public int Id { get; set; }

    public int AsignaturaId { get; set; }
    public Asignatura Asignatura { get; set; } = null!;

    /// <summary>Contenido del sílabo extraído del PDF.</summary>
    public string Contenido { get; set; } = string.Empty;

    /// <summary>Resultados de aprendizaje declarados en el plan.</summary>
    public string ResultadosAprendizaje { get; set; } = string.Empty;

    public DateTime FechaCarga { get; set; } = DateTime.UtcNow;
}
