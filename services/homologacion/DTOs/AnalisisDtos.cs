using System.ComponentModel.DataAnnotations;

namespace HomologaUTA.Homologacion.DTOs;

/// <summary>
/// Asignaturas de la solicitud que se van a analizar. Si se envía vacío (o no se envía
/// el cuerpo), el servicio toma las asignaturas reales de la solicitud desde Solicitudes.
/// </summary>
public class AnalizarSolicitudRequest
{
    public List<AsignaturaAnalisisDto> Asignaturas { get; set; } = new();
}

/// <summary>Asignatura de origen y su equivalente en destino, con el contenido de cada una.</summary>
public class AsignaturaAnalisisDto
{
    [Required(ErrorMessage = "El nombre de la asignatura de origen es obligatorio.")]
    public string AsignaturaOrigen { get; set; } = string.Empty;

    [Required(ErrorMessage = "El contenido de la asignatura de origen es obligatorio.")]
    public string ContenidoOrigen { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre de la asignatura de destino es obligatorio.")]
    public string AsignaturaDestino { get; set; } = string.Empty;

    [Required(ErrorMessage = "El contenido de la asignatura de destino es obligatorio.")]
    public string ContenidoDestino { get; set; } = string.Empty;
}

/// <summary>Resultado del análisis de una solicitud.</summary>
public class ResultadoAnalisisDto
{
    public int SolicitudId { get; set; }

    /// <summary>Estado en el que queda la solicitud: PENDIENTE_REVISION.</summary>
    public string Estado { get; set; } = string.Empty;

    public DateTime FechaAnalisis { get; set; }

    /// <summary>false si el análisis se guardó pero RabbitMQ no estaba disponible para publicar el evento.</summary>
    public bool EventoPublicado { get; set; }

    /// <summary>true si el servicio de Solicitudes confirmó el cambio a PENDIENTE_REVISION.</summary>
    public bool EstadoSolicitudActualizado { get; set; }

    public List<ResultadoAnalisisAsignaturaDto> Asignaturas { get; set; } = new();
}

public class ResultadoAnalisisAsignaturaDto
{
    public string AsignaturaOrigen { get; set; } = string.Empty;
    public string AsignaturaDestino { get; set; } = string.Empty;
    public int PorcentajeContenido { get; set; }
    public int PorcentajeProfundidad { get; set; }
    public int PorcentajeHoras { get; set; }

    /// <summary>HOMOLOGABLE, NO_HOMOLOGABLE o REQUIERE_REVISION.</summary>
    public string Decision { get; set; } = string.Empty;

    public string Justificacion { get; set; } = string.Empty;
    public bool RequiereRevision { get; set; }
}

/// <summary>Resultado que ve el estudiante: datos de su solicitud y la propuesta del agente.</summary>
public class ResultadoSolicitudDto
{
    public int SolicitudId { get; set; }
    public string Numero { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Modulo { get; set; } = "Agente interno";

    /// <summary>PENDIENTE_REVISION o INFORME_EMITIDO.</summary>
    public string Estado { get; set; } = string.Empty;

    public string Estudiante { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
    public string CarreraOrigen { get; set; } = string.Empty;
    public string CarreraDestino { get; set; } = string.Empty;
    public string ModuloSolicitud { get; set; } = "INTERNO";
    public string? UniversidadOrigen { get; set; }
    public string? UniversidadDestino { get; set; }
    public DateTime? FechaAnalisis { get; set; }
    public DateTime? FechaInforme { get; set; }
    public string? CoordinadorCorreo { get; set; }

    public List<AsignaturaResultadoSolicitudDto> Asignaturas { get; set; } = new();
}

public class AsignaturaResultadoSolicitudDto
{
    public int Id { get; set; }
    public string AsignaturaOrigen { get; set; } = string.Empty;
    public string AsignaturaDestino { get; set; } = string.Empty;
    public int PorcentajeContenido { get; set; }
    public int PorcentajeProfundidad { get; set; }
    public int PorcentajeHoras { get; set; }

    /// <summary>HOMOLOGABLE, NO_HOMOLOGABLE o REQUIERE_REVISION.</summary>
    public string Decision { get; set; } = string.Empty;

    public string Justificacion { get; set; } = string.Empty;
    public bool RequiereRevision { get; set; }

    /// <summary>Decisión final del Coordinador, si ya emitió el informe.</summary>
    public string? DecisionCoordinador { get; set; }
    public string? ObservacionCoordinador { get; set; }
}