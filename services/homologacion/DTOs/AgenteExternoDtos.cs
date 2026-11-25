using System.ComponentModel.DataAnnotations;

namespace HomologaUTA.Homologacion.DTOs;

/// <summary>
/// Lo que se envía al agente externo (contrato HU-09, sección 3; ampliado en HU-22).
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

    /// <summary>
    /// Materias aprobadas del estudiante que pueden usarse en una combinación (HU-22).
    /// El agente solo puede proponer materias de esta lista (o de las permitidas por destino).
    /// </summary>
    public List<string> MateriasOrigen { get; set; } = new();

    /// <summary>
    /// Asignaturas de destino que resultaron candidatas en la preselección (HU-21). Si viene
    /// vacío, el agente externo trabaja con los sílabos del bloque <see cref="Documentos"/>.
    /// </summary>
    public List<MateriaDestinoAgenteExternoDto> MateriasDestino { get; set; } = new();

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

/// <summary>Asignatura de destino candidata que se ofrece al agente externo (HU-21/HU-22).</summary>
public class MateriaDestinoAgenteExternoDto
{
    public int AsignaturaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Area { get; set; }
    public string? Nivel { get; set; }
    public string Contenido { get; set; } = string.Empty;

    /// <summary>Resultados de aprendizaje y horas de la asignatura de FISEI (para profundidad y carga horaria).</summary>
    public string? ResultadosAprendizaje { get; set; }
    public int? Horas { get; set; }

    /// <summary>Materias aprobadas que la preselección considera candidatas para este destino.</summary>
    public List<string> MateriasOrigenPermitidas { get; set; } = new();

    /// <summary>Notas de las materias aprobadas, para que el informe muestre el detalle.</summary>
    public Dictionary<string, decimal?> NotasMateriasOrigen { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Lo que responde el agente externo (contrato HU-09, sección 4; ampliado en HU-22).</summary>
public class AgenteExternoResponse
{
    public int SolicitudId { get; set; }
    public bool AnalisisCompletado { get; set; }
    public List<AsignaturaAgenteExternoDto> Asignaturas { get; set; } = new();
    public string? ObservacionesGenerales { get; set; }
}

public class AsignaturaAgenteExternoDto
{
    /// <summary>Se conserva por retrocompatibilidad con el contrato anterior.</summary>
    public string NombreOrigen { get; set; } = string.Empty;

    public string NombreDestino { get; set; } = string.Empty;

    /// <summary>Id de la asignatura de destino en el catálogo (HU-22).</summary>
    public int? AsignaturaDestinoId { get; set; }

    /// <summary>
    /// Una o dos materias de origen que cubren la asignatura de destino (HU-22). Si el agente
    /// no las envía, se usa <see cref="NombreOrigen"/>.
    /// </summary>
    public List<string> MateriasOrigen { get; set; } = new();

    /// <summary>La decisión la toma el agente. No se valida contra ninguna regla ni umbral.</summary>
    public string Decision { get; set; } = string.Empty;

    public string Justificacion { get; set; } = string.Empty;
    public int PorcentajeSimilitud { get; set; }
}
