namespace HomologaUTA.Homologacion.Models;

/// <summary>
/// Resolución del Consejo Directivo (HU-17). El sistema solo deja constancia de lo que
/// resolvió el Consejo: no reemplaza el trámite oficial.
/// </summary>
public class ResolucionConsejo
{
    public int Id { get; set; }

    public int AnalisisSolicitudId { get; set; }
    public AnalisisSolicitud AnalisisSolicitud { get; set; } = null!;

    /// <summary>Id de la solicitud en el servicio de Solicitudes.</summary>
    public int SolicitudId { get; set; }

    public string NumeroResolucion { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }

    /// <summary>Observación general del Consejo.</summary>
    public string? Observacion { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    /// <summary>Correo del usuario que registró la resolución.</summary>
    public string RegistradoPor { get; set; } = string.Empty;

    public List<DecisionResolucion> Decisiones { get; set; } = new();
}

/// <summary>Decisión final del Consejo para una asignatura del análisis.</summary>
public class DecisionResolucion
{
    public int Id { get; set; }

    public int ResolucionConsejoId { get; set; }
    public ResolucionConsejo ResolucionConsejo { get; set; } = null!;

    /// <summary>Id de la fila del análisis (AnalisisAsignatura).</summary>
    public int AsignaturaId { get; set; }

    /// <summary>HOMOLOGABLE o NO_HOMOLOGABLE.</summary>
    public string DecisionFinal { get; set; } = string.Empty;
}
