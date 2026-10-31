using System.ComponentModel.DataAnnotations;

namespace HomologaUTA.Homologacion.DTOs;

/// <summary>Datos de la solicitud sobre los que se ejecutan los validadores de reglas objetivas.</summary>
public class ValidarSolicitudRequest
{
    /// <summary>Tipo de solicitud: "Cambio de carrera", "Cambio de universidad" o "Movilidad".</summary>
    [Required(ErrorMessage = "El tipo de solicitud es obligatorio.")]
    public string Tipo { get; set; } = string.Empty;

    /// <summary>true si la universidad de origen es extranjera (RN-06 exige documentos apostillados).</summary>
    public bool EsUniversidadExtranjera { get; set; }

    /// <summary>Requisitos del estudiante. Obligatorio solo en un cambio de universidad (RN-05).</summary>
    public RequisitosEstudianteDto? Requisitos { get; set; }

    /// <summary>Documentación que trae la solicitud (RN-06).</summary>
    public DocumentacionDto Documentacion { get; set; } = new();

    /// <summary>Asignaturas de la solicitud con sus datos académicos.</summary>
    public List<AsignaturaValidarDto> Asignaturas { get; set; } = new();
}

/// <summary>Requisitos del estudiante para un cambio de universidad (RN-05).</summary>
public class RequisitosEstudianteDto
{
    /// <summary>true si el estudiante ya tuvo cambios de carrera previos.</summary>
    public bool TieneCambiosCarreraPrevios { get; set; }

    /// <summary>Períodos académicos cursados.</summary>
    [Range(0, 50, ErrorMessage = "Los períodos cursados deben estar entre 0 y 50.")]
    public int PeriodosCursados { get; set; }
}

/// <summary>Documentación presentada con la solicitud (RN-06).</summary>
public class DocumentacionDto
{
    public bool TieneMallaOrigen { get; set; }
    public bool TieneHistorialAcademico { get; set; }
    public bool TieneSilabos { get; set; }

    /// <summary>Solo se exige en universidades extranjeras.</summary>
    public bool DocumentosApostillados { get; set; }
}

/// <summary>Datos académicos de una asignatura que se quiere homologar.</summary>
public class AsignaturaValidarDto
{
    /// <summary>Id de la asignatura en el catálogo (opcional, se devuelve tal cual en el resultado).</summary>
    public int? Id { get; set; }

    [Required(ErrorMessage = "El nombre de la asignatura es obligatorio.")]
    public string Nombre { get; set; } = string.Empty;

    /// <summary>true si la asignatura fue aprobada. Obligatorio: si falta, la petición se rechaza.</summary>
    [Required(ErrorMessage = "Indique si la asignatura fue aprobada.")]
    public bool? Aprobada { get; set; }

    /// <summary>Años transcurridos desde la aprobación (RN-01).</summary>
    [Range(0, 100, ErrorMessage = "Los años desde la aprobación deben estar entre 0 y 100.")]
    public int AniosDesdeAprobacion { get; set; }

    /// <summary>Número de matrícula con el que se aprobó: 1, 2 o 3 (RN-04).</summary>
    [Range(1, 3, ErrorMessage = "El número de matrícula debe ser 1, 2 o 3.")]
    public int NumeroMatricula { get; set; } = 1;
}