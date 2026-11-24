using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HomologaUTA.Homologacion.DTOs;

// ------------------------------------------------------------------
// HU-19: memoria del agente (respuesta del Catálogo/Solicitudes)
// ------------------------------------------------------------------

/// <summary>Estado de la memoria del agente.</summary>
public class MemoriaEstadoDto
{
    public string Huella { get; set; } = string.Empty;
    public DateTime FechaCarga { get; set; }
    public int TotalCarreras { get; set; }
    public int TotalAsignaturas { get; set; }
}

/// <summary>Asignatura tal como vive en la memoria del agente.</summary>
public class MemoriaAsignaturaDto
{
    public int AsignaturaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Nivel { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public int Creditos { get; set; }
    public int Horas { get; set; }
    public string ContenidosMinimos { get; set; } = string.Empty;
}

// ------------------------------------------------------------------
// HU-20: extracción del récord y materias aprobadas
// ------------------------------------------------------------------

public class MateriaRecordDto
{
    public string Nombre { get; set; } = string.Empty;
    public decimal? Nota { get; set; }
    public string EstadoSegunDocumento { get; set; } = string.Empty;
    public string? Periodo { get; set; }
}

public class ResultadoExtraccionRecordDto
{
    public List<MateriaAprobadaDto> Materias { get; set; } = new();
    public bool RequiereRevision { get; set; }
    public string? Motivo { get; set; }

    /// <summary>Nombre real del titular, leído del récord ya guardado en la solicitud.</summary>
    public string? NombreEstudiante { get; set; }

    /// <summary>Cédula real del titular, leída del récord ya guardado en la solicitud.</summary>
    public string? CedulaEstudiante { get; set; }
}

public class MateriaAprobadaDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    [Range(0, 20, ErrorMessage = "La nota debe estar entre 0 y 20.")]
    public decimal? Nota { get; set; }

    public string EstadoSegunDocumento { get; set; } = string.Empty;
    public string? Periodo { get; set; }
}

/// <summary>Lista corregida por el estudiante; reemplaza la extracción anterior.</summary>
public class ActualizarMateriasAprobadasRequest
{
    [Required(ErrorMessage = "Debe enviar la lista de materias.")]
    public List<MateriaAprobadaInputDto> Materias { get; set; } = new();
}

public class MateriaAprobadaInputDto
{
    [Required(ErrorMessage = "El nombre de la materia es obligatorio.")]
    [MaxLength(200, ErrorMessage = "El nombre no puede pasar de 200 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Range(0, 20, ErrorMessage = "La nota debe estar entre 0 y 20.")]
    public decimal? Nota { get; set; }

    public string? EstadoSegunDocumento { get; set; }
    public string? Periodo { get; set; }
}

// ------------------------------------------------------------------
// HU-21: preselección
// ------------------------------------------------------------------

public class PreseleccionDto
{
    public List<ParPreseleccionDto> Pares { get; set; } = new();
    public List<MateriaSinCandidataDto> SinCandidata { get; set; } = new();
}

public class ParPreseleccionDto
{
    public string MateriaOrigen { get; set; } = string.Empty;
    public decimal? Nota { get; set; }
    public string? Area { get; set; }
    public int? Nivel { get; set; }
    public List<CandidataDestinoDto> CandidatasDestino { get; set; } = new();
}

public class CandidataDestinoDto
{
    public int AsignaturaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Nivel { get; set; } = string.Empty;
    public int NivelNumerico { get; set; }
    public string ContenidosMinimos { get; set; } = string.Empty;
}

public class MateriaSinCandidataDto
{
    public string MateriaOrigen { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;
}

// ------------------------------------------------------------------
// HU-17: resolución del Consejo Directivo
// ------------------------------------------------------------------

public class RegistrarResolucionRequest
{
    [Required(ErrorMessage = "El número de resolución es obligatorio.")]
    [MaxLength(100, ErrorMessage = "El número de resolución no puede pasar de 100 caracteres.")]
    public string NumeroResolucion { get; set; } = string.Empty;

    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    [Required(ErrorMessage = "Debe enviar la decisión final de cada asignatura.")]
    [MinLength(1, ErrorMessage = "Debe enviar al menos una asignatura.")]
    public List<DecisionResolucionInputDto> Decisiones { get; set; } = new();

    [MaxLength(2000, ErrorMessage = "La observación no puede pasar de 2000 caracteres.")]
    public string? Observacion { get; set; }
}

public class DecisionResolucionInputDto
{
    [Range(1, int.MaxValue, ErrorMessage = "El id de la asignatura no es válido.")]
    public int AsignaturaId { get; set; }

    [Required(ErrorMessage = "La decisión final es obligatoria.")]
    public string DecisionFinal { get; set; } = string.Empty;
}

public class ResolucionDto
{
    public int Id { get; set; }
    public int SolicitudId { get; set; }
    public string NumeroResolucion { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string? Observacion { get; set; }
    public DateTime FechaRegistro { get; set; }
    public string RegistradoPor { get; set; } = string.Empty;
    public string EstadoSolicitud { get; set; } = string.Empty;
    public List<DecisionResolucionDto> Decisiones { get; set; } = new();
}

public class DecisionResolucionDto
{
    public int AsignaturaId { get; set; }
    public string AsignaturaOrigen { get; set; } = string.Empty;
    public string AsignaturaDestino { get; set; } = string.Empty;
    public string DecisionFinal { get; set; } = string.Empty;
    public bool Combinada { get; set; }
    public List<string> MateriasOrigen { get; set; } = new();
}

// ------------------------------------------------------------------
// HU-22: materias de origen en los resultados
// ------------------------------------------------------------------

public class MateriaOrigenDto
{
    public string Nombre { get; set; } = string.Empty;
    public decimal? Nota { get; set; }
}
