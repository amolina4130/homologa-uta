namespace HomologaUTA.Homologacion.Models;

/// <summary>
/// Materia aprobada que se extrajo del récord académico del estudiante (HU-20).
/// Se guarda por solicitud y el estudiante puede corregirla mientras la solicitud siga REGISTRADA.
/// </summary>
public class MateriaAprobada
{
    public int Id { get; set; }

    /// <summary>Id de la solicitud en el servicio de Solicitudes.</summary>
    public int SolicitudId { get; set; }

    public string Nombre { get; set; } = string.Empty;

    /// <summary>Nota tal como aparece en el documento. Puede faltar si el récord no la muestra.</summary>
    public decimal? Nota { get; set; }

    /// <summary>Lo que dice el documento: aprobado, reprobado, etc.</summary>
    public string EstadoSegunDocumento { get; set; } = string.Empty;

    public string? Periodo { get; set; }

    public DateTime FechaExtraccion { get; set; } = DateTime.UtcNow;
}
