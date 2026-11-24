namespace HomologaUTA.Homologacion.Reportes;

/// <summary>Todo lo que lleva el informe, ya reunido; la plantilla solo lo dibuja.</summary>
public class InformeDatos
{
    public int SolicitudId { get; set; }
    public string NumeroProceso { get; set; } = string.Empty;
    public string EstudianteCorreo { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
    public string CarreraOrigen { get; set; } = string.Empty;
    public string CarreraDestino { get; set; } = string.Empty;

    /// <summary>INTERNO o EXTERNO.</summary>
    public string Modulo { get; set; } = "INTERNO";
    public string? UniversidadOrigen { get; set; }
    public string? UniversidadDestino { get; set; }

    public DateTime FechaAnalisis { get; set; }

    /// <summary>Null mientras el Coordinador no emita su informe técnico.</summary>
    public string? CoordinadorCorreo { get; set; }
    public DateTime? FechaInforme { get; set; }

    public List<InformeAsignatura> Asignaturas { get; set; } = new();

    /// <summary>Null mientras el Consejo no registre su resolución: esa sección se omite.</summary>
    public InformeResolucion? Resolucion { get; set; }
}

public class InformeAsignatura
{
    public string AsignaturaOrigen { get; set; } = string.Empty;
    public string AsignaturaDestino { get; set; } = string.Empty;
    public int PorcentajeContenido { get; set; }
    public int PorcentajeProfundidad { get; set; }
    public int PorcentajeHoras { get; set; }

    /// <summary>Propuesta del agente: HOMOLOGABLE, NO_HOMOLOGABLE o REQUIERE_REVISION.</summary>
    public string Decision { get; set; } = string.Empty;
    public string Justificacion { get; set; } = string.Empty;

    public string? DecisionCoordinador { get; set; }
    public string? ObservacionCoordinador { get; set; }
}

public class InformeResolucion
{
    public string NumeroResolucion { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string? Observacion { get; set; }
    public List<InformeDecisionFinal> Decisiones { get; set; } = new();
}

public class InformeDecisionFinal
{
    public string Asignatura { get; set; } = string.Empty;
    public string DecisionFinal { get; set; } = string.Empty;
}
