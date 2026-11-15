namespace HomologaUTA.Homologacion.DTOs;

/// <summary>Plan analítico que devuelve el Catálogo (HU-13).</summary>
public class PlanAnaliticoDto
{
    public int AsignaturaId { get; set; }
    public string Contenido { get; set; } = string.Empty;
    public string ResultadosAprendizaje { get; set; } = string.Empty;
    public DateTime FechaCarga { get; set; }
}

/// <summary>Estado de la memoria del agente (HU-19).</summary>
public class MemoriaEstadoDto
{
    public string Huella { get; set; } = string.Empty;
    public DateTime FechaCarga { get; set; }
    public int TotalCarreras { get; set; }
    public int TotalAsignaturas { get; set; }
}

/// <summary>Asignatura de la memoria del agente (HU-19).</summary>
public class MemoriaAsignaturaDto
{
    public int AsignaturaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Nivel { get; set; }
    public string Area { get; set; } = string.Empty;
    public int Creditos { get; set; }
    public int Horas { get; set; }
    public string ContenidosMinimos { get; set; } = string.Empty;
}

/// <summary>Equivalencia registrada entre dos asignaturas de carreras distintas (HU-14).</summary>
public class EquivalenciaDto
{
    public int Id { get; set; }
    public int CarreraOrigenId { get; set; }
    public string CarreraOrigen { get; set; } = string.Empty;
    public int AsignaturaOrigenId { get; set; }
    public string AsignaturaOrigen { get; set; } = string.Empty;
    public int CarreraDestinoId { get; set; }
    public string CarreraDestino { get; set; } = string.Empty;
    public int AsignaturaDestinoId { get; set; }
    public string AsignaturaDestino { get; set; } = string.Empty;
    public bool Activa { get; set; }
}

/// <summary>Detalle completo de una solicitud (endpoint interno de Solicitudes).</summary>
public class SolicitudDetalleInfoDto
{
    public int Id { get; set; }
    public string NumeroProceso { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Modulo { get; set; } = "INTERNO";
    public int CarreraOrigenId { get; set; }
    public string CarreraOrigen { get; set; } = string.Empty;
    public int CarreraDestinoId { get; set; }
    public string CarreraDestino { get; set; } = string.Empty;
    public string? UniversidadOrigen { get; set; }
    public string? UniversidadDestino { get; set; }
    public string EstudianteCorreo { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
    public string Estado { get; set; } = string.Empty;
    public bool Editable { get; set; }
    public List<SolicitudAsignaturaInfoDto> Asignaturas { get; set; } = new();
}

public class SolicitudAsignaturaInfoDto
{
    public int AsignaturaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Nivel { get; set; } = string.Empty;
    public int Creditos { get; set; }
    public int Horas { get; set; }
}

/// <summary>Materia aprobada guardada (HU-20).</summary>
public class MateriaAprobadaDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal? Nota { get; set; }
    public string? EstadoSegunDocumento { get; set; }
    public string? Periodo { get; set; }
    public int? AsignaturaId { get; set; }
}

/// <summary>Fila que envía el estudiante al corregir sus materias aprobadas.</summary>
public class MateriaAprobadaRequest
{
    public int? Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal? Nota { get; set; }
    public string? EstadoSegunDocumento { get; set; }
    public string? Periodo { get; set; }
    public int? AsignaturaId { get; set; }
}

/// <summary>Respuesta de la extracción del récord académico (HU-20).</summary>
public class ExtraerRecordResponse
{
    public List<MateriaAprobadaDto> Materias { get; set; } = new();
    public bool RequiereRevision { get; set; }
    public string? Motivo { get; set; }
}

/// <summary>Texto del récord académico que devuelve el servicio de Documentos.</summary>
public class DocumentoTextoDto
{
    public int DocumentoId { get; set; }
    public string TipoDocumento { get; set; } = string.Empty;
    public string NombreArchivo { get; set; } = string.Empty;
    public string TextoExtraido { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
}

/// <summary>Resultado de la preselección de candidatas (HU-21).</summary>
public class PreseleccionResponse
{
    public List<PreseleccionParDto> Pares { get; set; } = new();
    public List<string> SinCandidata { get; set; } = new();
}

public class PreseleccionParDto
{
    public string MateriaOrigen { get; set; } = string.Empty;
    public string AreaOrigen { get; set; } = string.Empty;
    public List<CandidataDestinoDto> CandidatasDestino { get; set; } = new();
}

public class CandidataDestinoDto
{
    public int AsignaturaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public int Nivel { get; set; }
}

/// <summary>Decisión final que registra el Consejo Directivo (HU-17).</summary>
public class ResolucionRequest
{
    public string NumeroResolucion { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string? Observacion { get; set; }
    public List<ResolucionDecisionRequest> Decisiones { get; set; } = new();
}

public class ResolucionDecisionRequest
{
    public int AsignaturaId { get; set; }
    public string DecisionFinal { get; set; } = string.Empty;
}

public class ResolucionResponse
{
    public int Id { get; set; }
    public int SolicitudId { get; set; }
    public string NumeroResolucion { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string? Observacion { get; set; }
    public DateTime FechaRegistro { get; set; }
    public List<ResolucionDecisionResponse> Decisiones { get; set; } = new();
}

public class ResolucionDecisionResponse
{
    public int AsignaturaId { get; set; }
    public string DecisionFinal { get; set; } = string.Empty;
}

/// <summary>Petición del análisis que usa la memoria del agente y las equivalencias (HU-14/HU-21/HU-22).</summary>
public class AnalisisMemoriaRequest
{
    /// <summary>Asignaturas de origen a analizar; si no se envía, se analizan todas las de la solicitud.</summary>
    public List<int>? AsignaturasIds { get; set; }

    /// <summary>Combinaciones explícitas de una o dos materias de origen para una de destino.</summary>
    public List<CombinacionRequest> Combinaciones { get; set; } = new();
}

public class CombinacionRequest
{
    public int AsignaturaDestinoId { get; set; }
    public List<int> MateriasOrigenIds { get; set; } = new();
}

/// <summary>Resultado del análisis guardado, tal como lo consume el frontend.</summary>
public class ResultadoDetalleDto
{
    public int SolicitudId { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime FechaAnalisis { get; set; }
    public string? MemoriaHuella { get; set; }
    public List<ResultadoAnalisisAsignaturaDto> Asignaturas { get; set; } = new();
}
