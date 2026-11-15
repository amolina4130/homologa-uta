using System.ComponentModel.DataAnnotations;

namespace HomologaUTA.Homologacion.DTOs;

/// <summary>
/// Lo que se envía al agente externo (contrato HU-09, sección 3).
/// El cuerpo de la petición es opcional: si falta un dato, el servicio lo completa con la
/// solicitud real y con el texto extraído de los documentos procesados.
/// </summary>
public class AgenteExternoRequest
{
    /// <summary>Se toma del id de la ruta; lo que se envíe aquí se ignora.</summary>
    public int SolicitudId { get; set; }

    public string Estudiante { get; set; } = string.Empty;

    public string CarreraOrigen { get; set; } = string.Empty;

    public string CarreraDestino { get; set; } = string.Empty;

    public DocumentosAgenteExternoDto Documentos { get; set; } = new();
}

public class DocumentosAgenteExternoDto
{
    public string MallaOrigen { get; set; } = string.Empty;
    public string HistorialAcademico { get; set; } = string.Empty;

    public List<SilaboAgenteExternoDto> Silabos { get; set; } = new();
}

public class SilaboAgenteExternoDto
{
    [Required(ErrorMessage = "El nombre de la asignatura del sílabo es obligatorio.")]
    public string Asignatura { get; set; } = string.Empty;

    [Required(ErrorMessage = "El contenido del sílabo es obligatorio.")]
    public string Contenido { get; set; } = string.Empty;
}

/// <summary>Lo que responde el agente externo (contrato HU-09, sección 4).</summary>
public class AgenteExternoResponse
{
    public int SolicitudId { get; set; }
    public bool AnalisisCompletado { get; set; }
    public List<AsignaturaAgenteExternoDto> Asignaturas { get; set; } = new();
    public string? ObservacionesGenerales { get; set; }
}

public class AsignaturaAgenteExternoDto
{
    public string NombreOrigen { get; set; } = string.Empty;
    public string NombreDestino { get; set; } = string.Empty;

    /// <summary>La decisión la toma el agente. No se valida contra ninguna regla ni umbral.</summary>
    public string Decision { get; set; } = string.Empty;

    public string Justificacion { get; set; } = string.Empty;
    public int PorcentajeSimilitud { get; set; }
}
