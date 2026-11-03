using System.ComponentModel.DataAnnotations;

namespace HomologaUTA.Homologacion.DTOs;

/// <summary>Datos de la solicitud que vienen del servicio de Solicitudes.</summary>
public class SolicitudInfoDto
{
    public int Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string EstudianteCorreo { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
    public string CarreraOrigen { get; set; } = string.Empty;
    public string CarreraDestino { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
}

/// <summary>Fila de la lista de solicitudes pendientes de revisión.</summary>
public class PendienteDto
{
    public int SolicitudId { get; set; }

    /// <summary>Fecha en que el estudiante registró la solicitud.</summary>
    public DateTime Fecha { get; set; }

    public string Estudiante { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string CarreraOrigen { get; set; } = string.Empty;
    public string CarreraDestino { get; set; } = string.Empty;

    /// <summary>Fecha en que terminó el análisis del agente.</summary>
    public DateTime FechaAnalisis { get; set; }

    public int TotalAsignaturas { get; set; }
}

/// <summary>Detalle de una solicitud con la propuesta del agente por asignatura.</summary>
public class DetalleSolicitudDto
{
    public int SolicitudId { get; set; }

    /// <summary>PENDIENTE_REVISION o INFORME_EMITIDO.</summary>
    public string Estado { get; set; } = string.Empty;

    public DateTime Fecha { get; set; }
    public string Estudiante { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string CarreraOrigen { get; set; } = string.Empty;
    public string CarreraDestino { get; set; } = string.Empty;
    public DateTime FechaAnalisis { get; set; }

    /// <summary>Solo si el informe ya se emitió.</summary>
    public string? CoordinadorCorreo { get; set; }
    public DateTime? FechaInforme { get; set; }

    public List<DetalleAsignaturaDto> Asignaturas { get; set; } = new();
}

public class DetalleAsignaturaDto
{
    /// <summary>Id de la asignatura analizada; se usa al emitir el informe.</summary>
    public int Id { get; set; }

    public string AsignaturaOrigen { get; set; } = string.Empty;
    public string AsignaturaDestino { get; set; } = string.Empty;

    public PropuestaAgenteDto Propuesta { get; set; } = new();

    /// <summary>Decisión y observación del Coordinador; null mientras no emita el informe.</summary>
    public string? DecisionCoordinador { get; set; }
    public string? ObservacionCoordinador { get; set; }
}

/// <summary>Lo que propuso el agente para la asignatura.</summary>
public class PropuestaAgenteDto
{
    /// <summary>HOMOLOGABLE, NO_HOMOLOGABLE o REQUIERE_REVISION.</summary>
    public string Decision { get; set; } = string.Empty;

    public string Justificacion { get; set; } = string.Empty;
    public int PorcentajeContenido { get; set; }
    public int PorcentajeProfundidad { get; set; }
    public int PorcentajeHoras { get; set; }
    public bool RequiereRevision { get; set; }
}

/// <summary>Informe técnico: una decisión y una observación por cada asignatura de la solicitud.</summary>
public class EmitirInformeRequest
{
    [Required(ErrorMessage = "Debe enviar la decisión de cada asignatura.")]
    [MinLength(1, ErrorMessage = "Debe enviar al menos una asignatura.")]
    public List<InformeAsignaturaDto> Asignaturas { get; set; } = new();
}

public class InformeAsignaturaDto
{
    [Range(1, int.MaxValue, ErrorMessage = "El id de la asignatura no es válido.")]
    public int AsignaturaId { get; set; }

    /// <summary>HOMOLOGABLE o NO_HOMOLOGABLE.</summary>
    [Required(ErrorMessage = "La decisión es obligatoria.")]
    public string Decision { get; set; } = string.Empty;

    [Required(ErrorMessage = "La observación es obligatoria.")]
    [MaxLength(1000, ErrorMessage = "La observación no puede pasar de 1000 caracteres.")]
    public string Observacion { get; set; } = string.Empty;
}

/// <summary>Informe técnico guardado.</summary>
public class ResultadoInformeDto
{
    public int SolicitudId { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string CoordinadorCorreo { get; set; } = string.Empty;
    public DateTime FechaInforme { get; set; }
    public List<InformeAsignaturaResultadoDto> Asignaturas { get; set; } = new();
}

public class InformeAsignaturaResultadoDto
{
    public int AsignaturaId { get; set; }
    public string AsignaturaOrigen { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string Observacion { get; set; } = string.Empty;
}