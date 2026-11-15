using System.ComponentModel.DataAnnotations;

namespace HomologaUTA.Homologacion.Models;

/// <summary>Estados en los que queda el análisis de una solicitud.</summary>
public static class EstadoAnalisis
{
    public const string PENDIENTE_REVISION = "PENDIENTE_REVISION";

    /// <summary>El Coordinador ya emitió su informe técnico.</summary>
    public const string INFORME_EMITIDO = "INFORME_EMITIDO";
}

/// <summary>Decisiones posibles para una asignatura.</summary>
public static class DecisionAnalisis
{
    public const string HOMOLOGABLE = "HOMOLOGABLE";
    public const string NO_HOMOLOGABLE = "NO_HOMOLOGABLE";

    /// <summary>El agente no pudo responder tras los reintentos: lo decide una persona.</summary>
    public const string REQUIERE_REVISION = "REQUIERE_REVISION";

    /// <summary>No se encontró una materia de destino candidata para la materia de origen.</summary>
    public const string SIN_EQUIVALENTE = "SIN_EQUIVALENTE";
}

/// <summary>Resultado del análisis de una solicitud completa.</summary>
public class AnalisisSolicitud
{
    public int Id { get; set; }

    /// <summary>Id de la solicitud en el servicio de Solicitudes.</summary>
    public int SolicitudId { get; set; }

    public string Estado { get; set; } = EstadoAnalisis.PENDIENTE_REVISION;
    public DateTime FechaAnalisis { get; set; } = DateTime.UtcNow;

    /// <summary>Huella de la memoria del agente con la que se hizo el análisis (HU-19).</summary>
    public string? MemoriaHuella { get; set; }

    /// <summary>Correo del Coordinador que emitió el informe técnico (null mientras no se emita).</summary>
    public string? CoordinadorCorreo { get; set; }

    /// <summary>Fecha en que se emitió el informe técnico (null mientras no se emita).</summary>
    public DateTime? FechaInforme { get; set; }

    public List<AnalisisAsignatura> Asignaturas { get; set; } = new();
}

/// <summary>Resultado del análisis de una asignatura de destino: porcentajes, decisión y justificación.</summary>
public class AnalisisAsignatura
{
    public int Id { get; set; }

    public int AnalisisSolicitudId { get; set; }
    public AnalisisSolicitud AnalisisSolicitud { get; set; } = null!;

    /// <summary>Id de la asignatura de destino en el catálogo (null si no se pudo asociar).</summary>
    public int? AsignaturaDestinoId { get; set; }

    public string AsignaturaOrigen { get; set; } = string.Empty;
    public string AsignaturaDestino { get; set; } = string.Empty;

    public int PorcentajeContenido { get; set; }
    public int PorcentajeProfundidad { get; set; }
    public int PorcentajeHoras { get; set; }

    /// <summary>Propuesta del agente: HOMOLOGABLE, NO_HOMOLOGABLE, REQUIERE_REVISION o SIN_EQUIVALENTE.</summary>
    public string Decision { get; set; } = string.Empty;

    public string Justificacion { get; set; } = string.Empty;
    public bool RequiereRevision { get; set; }

    /// <summary>true cuando la materia de destino se cubre con dos materias de origen (HU-22).</summary>
    public bool Combinada { get; set; }

    /// <summary>Regla que resolvió el resultado antes de llamar al agente (por ejemplo, equivalencia).</summary>
    public string? ReglaAplicada { get; set; }

    /// <summary>Una o dos materias de origen que cubren la materia de destino.</summary>
    public List<MateriaOrigenAnalisis> MateriasOrigen { get; set; } = new();

    /// <summary>Decisión del Coordinador en su informe técnico: HOMOLOGABLE o NO_HOMOLOGABLE.</summary>
    public string? DecisionCoordinador { get; set; }

    /// <summary>Observación del Coordinador para esta asignatura.</summary>
    public string? ObservacionCoordinador { get; set; }
}

/// <summary>Materia de origen (una o dos) que cubre una materia de destino (HU-22).</summary>
public class MateriaOrigenAnalisis
{
    public int Id { get; set; }

    public int AnalisisAsignaturaId { get; set; }
    public AnalisisAsignatura AnalisisAsignatura { get; set; } = null!;

    /// <summary>Id de la asignatura en el catálogo, cuando se conoce.</summary>
    public int? AsignaturaId { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public decimal? Nota { get; set; }

    public string? EstadoSegunDocumento { get; set; }
    public string? Periodo { get; set; }

    [Range(1, 2)]
    public int Orden { get; set; } = 1;
}

/// <summary>Materia aprobada extraída del récord académico del estudiante (HU-20).</summary>
public class MateriaAprobada
{
    public int Id { get; set; }

    public int SolicitudId { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public decimal? Nota { get; set; }

    /// <summary>Lo que dice el documento: APROBADO, REPROBADO, etc.</summary>
    public string? EstadoSegunDocumento { get; set; }

    public string? Periodo { get; set; }

    /// <summary>Id de la asignatura de origen en el catálogo, cuando se puede asociar.</summary>
    public int? AsignaturaId { get; set; }

    public DateTime FechaExtraccion { get; set; } = DateTime.UtcNow;
}

/// <summary>Resolución registrada por el Consejo Directivo (HU-17).</summary>
public class ResolucionConsejo
{
    public int Id { get; set; }

    /// <summary>Una sola resolución por solicitud.</summary>
    public int SolicitudId { get; set; }

    public string NumeroResolucion { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }

    public string? Observacion { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public List<ResolucionDecision> Decisiones { get; set; } = new();
}

/// <summary>Decisión final del Consejo para una asignatura.</summary>
public class ResolucionDecision
{
    public int Id { get; set; }

    public int ResolucionConsejoId { get; set; }
    public ResolucionConsejo ResolucionConsejo { get; set; } = null!;

    public int AsignaturaId { get; set; }

    public string DecisionFinal { get; set; } = string.Empty;

    public string? Observacion { get; set; }
}
