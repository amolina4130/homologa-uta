using System.ComponentModel.DataAnnotations;

namespace HomologaUTA.Homologacion.Models;

public class ResolucionConsejo
{
    public int Id { get; set; }
    
    public int SolicitudId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string NumeroResolucion { get; set; } = string.Empty;
    
    public DateTime Fecha { get; set; }
    
    [MaxLength(2000)]
    public string? Observacion { get; set; }
    
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    
    public string UsuarioRegistro { get; set; } = string.Empty;

    // Relación con las decisiones por asignatura
    public List<DecisionAsignaturaResolucion> Decisiones { get; set; } = new();
}

public class DecisionAsignaturaResolucion
{
    public int Id { get; set; }
    public int ResolucionConsejoId { get; set; }
    public int AsignaturaId { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string DecisionFinal { get; set; } = string.Empty;
}