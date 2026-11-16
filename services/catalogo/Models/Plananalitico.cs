namespace HomologaUTA.Catalogo.Models;

public class PlanAnalitico
{
    public int Id { get; set; }
    public int AsignaturaId { get; set; }
    public string Contenido { get; set; } = string.Empty;
    public string ResultadosAprendizaje { get; set; } = string.Empty;
    public DateTime FechaCarga { get; set; } = DateTime.UtcNow;
    public bool EsVigente { get; set; } = true;

    // Relación con la asignatura (si el catálogo maneja su propia BD)
    // public Asignatura Asignatura { get; set; } = null!;
}