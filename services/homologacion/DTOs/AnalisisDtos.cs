using System.ComponentModel.DataAnnotations;

namespace HomologaUTA.Homologacion.DTOs;

/// <summary>Asignaturas de la solicitud que se van a analizar.</summary>
public class AnalizarSolicitudRequest
{
    [Required(ErrorMessage = "Debe enviar las asignaturas a analizar.")]
    [MinLength(1, ErrorMessage = "Debe enviar al menos una asignatura.")]
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