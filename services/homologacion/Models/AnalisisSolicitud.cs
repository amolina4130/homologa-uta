namespace HomologaUTA.Homologacion.Models;

/// <summary>Estados en los que queda el análisis de una solicitud.</summary>
public static class EstadoAnalisis
{
    public const string PENDIENTE_REVISION = "PENDIENTE_REVISION";

    /// <summary>El Coordinador ya emitió su informe técnico.</summary>
    public const string INFORME_EMITIDO = "INFORME_EMITIDO";

    /// <summary>El Consejo Directivo ya registró su resolución (HU-17).</summary>
    public const string RESUELTA = "RESUELTA";
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

    /// <summary>Correo del Coordinador que emitió el informe técnico (null mientras no se emita).</summary>
    public string? CoordinadorCorreo { get; set; }

    /// <summary>Fecha en que se emitió el informe técnico (null mientras no se emita).</summary>
    public DateTime? FechaInforme { get; set; }

    // Datos del informe final del Coordinador. Se guardan para poder regenerar el PDF.
    public string? InformeFinalTexto { get; set; }
    public string? NombreEstudiante { get; set; }
    public string? CedulaEstudiante { get; set; }
    public string? NumeroMemorando { get; set; }
    public string? NombreFirmante { get; set; }

    public List<AnalisisAsignatura> Asignaturas { get; set; } = new();

    /// <summary>Resolución del Consejo Directivo; null mientras no se registre (HU-17).</summary>
    public ResolucionConsejo? Resolucion { get; set; }
}

/// <summary>Resultado del análisis de una asignatura: porcentajes, decisión y justificación.</summary>
public class AnalisisAsignatura
{
    public int Id { get; set; }

    public int AnalisisSolicitudId { get; set; }
    public AnalisisSolicitud AnalisisSolicitud { get; set; } = null!;

    public string AsignaturaOrigen { get; set; } = string.Empty;
    public string AsignaturaDestino { get; set; } = string.Empty;

    /// <summary>Id de la asignatura de destino en el catálogo (HU-21).</summary>
    public int? AsignaturaDestinoId { get; set; }

    /// <summary>Área de la materia de origen y de la asignatura de destino (HU-21).</summary>
    public string? AreaOrigen { get; set; }
    public string? AreaDestino { get; set; }

    /// <summary>true cuando la asignatura de destino se cubre con dos materias de origen (HU-22).</summary>
    public bool Combinada { get; set; }

    /// <summary>Una o dos materias de origen que cubren esta asignatura de destino.</summary>
    public List<MateriaOrigenResultado> MateriasOrigen { get; set; } = new();

    public int PorcentajeContenido { get; set; }
    public int PorcentajeProfundidad { get; set; }
    public int PorcentajeHoras { get; set; }

    /// <summary>Propuesta del agente: HOMOLOGABLE, NO_HOMOLOGABLE o REQUIERE_REVISION.</summary>
    public string Decision { get; set; } = string.Empty;

    public string Justificacion { get; set; } = string.Empty;
    public bool RequiereRevision { get; set; }

    /// <summary>Decisión del Coordinador en su informe técnico: HOMOLOGABLE o NO_HOMOLOGABLE.</summary>
    public string? DecisionCoordinador { get; set; }

    /// <summary>Observación del Coordinador para esta asignatura.</summary>
    public string? ObservacionCoordinador { get; set; }
}