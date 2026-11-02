namespace HomologaUTA.Homologacion.Models;

/// <summary>Estados en los que queda la solicitud después del análisis.</summary>
public static class EstadoAnalisis
{
    public const string PENDIENTE_REVISION = "PENDIENTE_REVISION";
}

/// <summary>Decisiones posibles para una asignatura.</summary>
public static class DecisionAnalisis
{
    public const string HOMOLOGABLE = "HOMOLOGABLE";
    public const string NO_HOMOLOGABLE = "NO_HOMOLOGABLE";

    /// <summary>El agente no pudo responder tras los reintentos: lo decide una persona.</summary>
    public const string REQUIERE_REVISION = "REQUIERE_REVISION";
}

/// <summary>Resultado del análisis de una solicitud completa.</summary>
public class AnalisisSolicitud
{
    public int Id { get; set; }

    /// <summary>Id de la solicitud en el servicio de Solicitudes.</summary>
    public int SolicitudId { get; set; }

    public string Estado { get; set; } = EstadoAnalisis.PENDIENTE_REVISION;
    public DateTime FechaAnalisis { get; set; } = DateTime.UtcNow;

    public List<AnalisisAsignatura> Asignaturas { get; set; } = new();
}

/// <summary>Resultado del análisis de una asignatura: porcentajes, decisión y justificación.</summary>
public class AnalisisAsignatura
{
    public int Id { get; set; }

    public int AnalisisSolicitudId { get; set; }
    public AnalisisSolicitud AnalisisSolicitud { get; set; } = null!;

    public string AsignaturaOrigen { get; set; } = string.Empty;
    public string AsignaturaDestino { get; set; } = string.Empty;

    public int PorcentajeContenido { get; set; }
    public int PorcentajeProfundidad { get; set; }
    public int PorcentajeHoras { get; set; }

    /// <summary>HOMOLOGABLE, NO_HOMOLOGABLE o REQUIERE_REVISION (la calcula el código, no el agente).</summary>
    public string Decision { get; set; } = string.Empty;

    public string Justificacion { get; set; } = string.Empty;
    public bool RequiereRevision { get; set; }
}