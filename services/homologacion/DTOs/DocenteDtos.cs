using System.ComponentModel.DataAnnotations;

namespace HomologaUTA.Homologacion.DTOs;

// ------------------------------------------------------------------
// Flujo Comité -> Coordinador -> Docentes -> informe final
// ------------------------------------------------------------------

/// <summary>Delegación de una solicitud a un docente, tal como la ve el frontend.</summary>
public class DelegacionDto
{
    public int Id { get; set; }
    public int SolicitudId { get; set; }
    public string DocenteCorreo { get; set; } = string.Empty;

    /// <summary>Nombre editable del docente que firma el informe.</summary>
    public string? NombreDocente { get; set; }

    /// <summary>Cédula editable del docente que firma el informe.</summary>
    public string? DocenteCedula { get; set; }

    /// <summary>Correo editable que se imprime en el informe.</summary>
    public string? DocenteCorreoInforme { get; set; }

    /// <summary>Correo del Coordinador que delegó la solicitud.</summary>
    public string CoordinadorCorreo { get; set; } = string.Empty;

    public string Estado { get; set; } = string.Empty;
    public DateTime FechaDelegacion { get; set; }
    public DateTime? FechaRevision { get; set; }
    public string? InformeTexto { get; set; }

    /// <summary>Correo de quien registró la revisión ( trazabilidad cuando ya revisó otro).</summary>
    public string? RevisadoPor { get; set; }

    /// <summary>APROBADO o NO_APROBADO según las asignaturas marcadas por el docente.</summary>
    public string? ResultadoDocente { get; set; }

    /// <summary>Texto del informe sugerido emitido por el docente que revisó primero.</summary>
    public string? InformeSugeridoTexto { get; set; }
}

/// <summary>Fila de la lista de solicitudes asignadas a un docente.</summary>
public class SolicitudDocenteDto
{
    public int SolicitudId { get; set; }
    public string NumeroProceso { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }

    /// <summary>Nombre real del estudiante; no es el correo del Comité.</summary>
    public string Estudiante { get; set; } = string.Empty;
    public string EstudianteCedula { get; set; } = string.Empty;
    public string ComiteCorreo { get; set; } = string.Empty;

    /// <summary>Coordinador que delegó la revisión a este docente.</summary>
    public string CoordinadorCorreo { get; set; } = string.Empty;

    /// <summary>Datos del firmante editable por el Docente antes de generar el informe.</summary>
    public string? NombreDocente { get; set; }
    public string? DocenteCedula { get; set; }
    public string? DocenteCorreoInforme { get; set; }

    /// <summary>INTERNO o EXTERNO.</summary>
    public string Modulo { get; set; } = "INTERNO";
    public string? UniversidadOrigen { get; set; }
    public string? UniversidadDestino { get; set; }

    public string CarreraOrigen { get; set; } = string.Empty;
    public string CarreraDestino { get; set; } = string.Empty;

    /// <summary>PENDIENTE, REVISADO o YA_REVISADO (cuando otro docente calificó primero).</summary>
    public string EstadoDelegacion { get; set; } = string.Empty;

    public string? RevisadoPor { get; set; }
    public bool PuedeEvaluar { get; set; }
    public int TotalAsignaturas { get; set; }

    /// <summary>APROBADO o NO_APROBADO, calculado del informe del docente que revisó primero.</summary>
    public string? ResultadoDocente { get; set; }

    /// <summary>Informe sugerido ya emitido, visible para los demás docentes asignados.</summary>
    public string? InformeSugeridoTexto { get; set; }
    public DateTime? FechaRevision { get; set; }

    /// <summary>Existe una sugerencia de IA generada, todavía no enviada al Coordinador.</summary>
    public bool TieneSugerencia { get; set; }

    /// <summary>La sugerencia en curso ya fue enviada como informe sugerido.</summary>
    public bool SugerenciaEnviada { get; set; }

    public DateTime? FechaSugerencia { get; set; }
    public string? SugerenciaGeneradaPor { get; set; }
}

/// <summary>Detalle real de la solicitud más la delegación y el informe sugerido.</summary>
public class DetalleDocenteDto
{
    public DetalleSolicitudDto Detalle { get; set; } = new();
    public DelegacionDto Delegacion { get; set; } = new();
    public string InformeSugeridoTexto { get; set; } = string.Empty;

    /// <summary>False cuando este docente u otro ya emitió la revisión.</summary>
    public bool PuedeEvaluar { get; set; }

    /// <summary>
    /// Sugerencia generada por IA, en curso o ya enviada. Es la misma para los dos docentes
    /// asignados: si otro la generó primero, este docente también la ve.
    /// </summary>
    public SugerenciaDocenteDto? Sugerencia { get; set; }

    /// <summary>Indica que la sugerencia visible fue generada por otro docente asignado.</summary>
    public bool SugerenciaEsDeOtroDocente { get; set; }

    /// <summary>Datos del firmante que el Docente puede editar en esta pantalla.</summary>
    public string NombreDocente { get; set; } = string.Empty;
    public string DocenteCedula { get; set; } = string.Empty;
    public string DocenteCorreoInforme { get; set; } = string.Empty;
}

/// <summary>Una asignatura dentro de la sugerencia que la IA propone al Docente.</summary>
public class SugerenciaAsignaturaDto
{
    /// <summary>Id de la asignatura del análisis; es el que usa el frontend para marcarla.</summary>
    public int AsignaturaId { get; set; }
    public string AsignaturaOrigen { get; set; } = string.Empty;
    public string AsignaturaDestino { get; set; } = string.Empty;
    public bool Homologable { get; set; }
    public string Justificacion { get; set; } = string.Empty;
    public int PorcentajeContenido { get; set; }
}

/// <summary>Borrador de la sugerencia del Docente: se genera primero y se envía después.</summary>
public class SugerenciaDocenteDto
{
    public int SolicitudId { get; set; }
    public int DelegacionId { get; set; }
    public string GeneradaPor { get; set; } = string.Empty;
    public string NombreDocente { get; set; } = string.Empty;
    public string DocenteCedula { get; set; } = string.Empty;
    public string DocenteCorreoInforme { get; set; } = string.Empty;
    public DateTime FechaGeneracion { get; set; }
    public List<SugerenciaAsignaturaDto> Asignaturas { get; set; } = new();
    public string? ObservacionGeneral { get; set; }
    public string? OpinionIA { get; set; }
    public string InformeTexto { get; set; } = string.Empty;

    /// <summary>True cuando la sugerencia ya se envió al Coordinador como informe sugerido.</summary>
    public bool Enviada { get; set; }
}

/// <summary>
/// Datos que el Docente puede corregir antes de generar o enviar su informe. Se guardan en la
/// delegación y se imprimen en el texto y en el PDF, sin depender del correo de acceso.
/// </summary>
public class DatosFirmaDocenteRequest
{
    [MaxLength(200, ErrorMessage = "El nombre del docente no puede pasar de 200 caracteres.")]
    public string? NombreDocente { get; set; }

    [MaxLength(50, ErrorMessage = "La cédula del docente no puede pasar de 50 caracteres.")]
    public string? DocenteCedula { get; set; }

    [MaxLength(200, ErrorMessage = "El correo del informe no puede pasar de 200 caracteres.")]
    public string? DocenteCorreoInforme { get; set; }

    /// <summary>
    /// Texto del informe editado manualmente por el Docente. Si se envía, sustituye al texto
    /// generado por la plantilla; así puede corregir sus datos directamente en el informe.
    /// </summary>
    [MaxLength(8000, ErrorMessage = "El informe no puede pasar de 8000 caracteres.")]
    public string? InformeTexto { get; set; }
}

/// <summary>Cuerpo opcional para regenerar la vista previa con las decisiones del docente.</summary>
public class GenerarSugerenciaRequest : DatosFirmaDocenteRequest
{
    public List<EvaluacionAsignaturaDto>? Asignaturas { get; set; }

    [MaxLength(2000, ErrorMessage = "La observación general no puede pasar de 2000 caracteres.")]
    public string? ObservacionGeneral { get; set; }

    /// <summary>
    /// Si es true se reutiliza el párrafo de IA ya generado (por ejemplo al añadir observaciones)
    /// y no se vuelve a llamar al modelo.
    /// </summary>
    public bool ReutilizarOpinion { get; set; } = true;
}

/// <summary>Evaluación de una asignatura enviada por el docente.</summary>
public class EvaluacionAsignaturaDto
{
    public int AsignaturaId { get; set; }
    public bool Homologable { get; set; }
    public string Justificacion { get; set; } = string.Empty;
}

public class EvaluarDocenteRequest : DatosFirmaDocenteRequest
{
    public List<EvaluacionAsignaturaDto> Asignaturas { get; set; } = new();

    [MaxLength(2000, ErrorMessage = "La observación general no puede pasar de 2000 caracteres.")]
    public string? ObservacionGeneral { get; set; }

    /// <summary>
    /// El Docente debe marcar explícitamente REVISADO antes de enviar. Guardar la sugerencia
    /// no cambia este estado: solo el envío manual lo registra.
    /// </summary>
    public string EstadoRevision { get; set; } = "PENDIENTE";
}

public class ResultadoEvaluacionDto
{
    public int DelegacionId { get; set; }
    public int SolicitudId { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string InformeTexto { get; set; } = string.Empty;
    public DateTime FechaRevision { get; set; }
}

public class ConsolidadoDto
{
    public DetalleSolicitudDto Detalle { get; set; } = new();
    public List<DelegacionDto> Delegaciones { get; set; } = new();

    /// <summary>Informe final guardado. Vacío hasta que el Coordinador lo emita.</summary>
    public string InformeFinalTexto { get; set; } = string.Empty;

    /// <summary>Informe sugerido del docente que revisó primero.</summary>
    public string InformeSugeridoTexto { get; set; } = string.Empty;

    /// <summary>Correo del docente que revisó primero, cuando ya hay informe sugerido.</summary>
    public string? RevisadoPor { get; set; }

    /// <summary>APROBADO o NO_APROBADO según el informe sugerido del docente.</summary>
    public string? ResultadoDocente { get; set; }

    /// <summary>PENDIENTE o REVISADO.</summary>
    public string EstadoRevisionDocente { get; set; } = "PENDIENTE";

    /// <summary>El informe final solo se habilita cuando ya existe el informe del docente.</summary>
    public bool PuedeGenerarInformeFinal { get; set; }

    public List<EvaluacionAsignaturaDto> EvaluacionesConsolidadas { get; set; } = new();

    /// <summary>Filas legibles del informe sugerido que el Docente envió al Coordinador.</summary>
    public List<FilaInformeSugeridoDto> InformeSugeridoAsignaturas { get; set; } = new();
}

/// <summary>Una fila de la tabla legible del informe sugerido del Docente.</summary>
public class FilaInformeSugeridoDto
{
    public int AsignaturaId { get; set; }
    public string AsignaturaOrigen { get; set; } = string.Empty;
    public string AsignaturaDestino { get; set; } = string.Empty;
    public string CarreraOrigen { get; set; } = string.Empty;
    public string CarreraDestino { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
    public string SeHomologa { get; set; } = string.Empty;
    public string Justificacion { get; set; } = string.Empty;
}

public class InformeFinalRequest
{
    public List<InformeAsignaturaDto> Asignaturas { get; set; } = new();
    public string? NombreEstudiante { get; set; }
    public string? CedulaEstudiante { get; set; }
    public string? NumeroMemorando { get; set; }
    public string? NombreFirmante { get; set; }
    /// <summary>Texto del informe final editado por el Coordinador. Si se envía, se usa tal cual al emitir.</summary>
    public string? InformeFinalTexto { get; set; }
}

/// <summary>Cuerpo del endpoint de delegación: { "correos": [...] }.</summary>
public class DelegarRequest
{
    public List<string> Correos { get; set; } = new();
}

/// <summary>Recomendación de homologación hecha por el agente interno para una asignatura.</summary>
public class RecomendacionMateriaDto
{
    /// <summary>Id de la asignatura del análisis (el que marca el Docente).</summary>
    public int AsignaturaId { get; set; }
    public string AsignaturaOrigen { get; set; } = string.Empty;

    /// <summary>Id de la asignatura destino del catálogo de la carrera destino.</summary>
    public int? AsignaturaDestinoId { get; set; }
    public string AsignaturaDestino { get; set; } = string.Empty;
    public bool Homologable { get; set; }
    public int PorcentajeContenido { get; set; }
    public string Justificacion { get; set; } = string.Empty;
    public int CreditosOrigen { get; set; }
    public int CreditosDestino { get; set; }
    public int HorasOrigen { get; set; }
    public int HorasDestino { get; set; }
}

/// <summary>
/// Resultado de "IA me recomienda qué materias homologar". Solo se ofrece en solicitudes
/// INTERNO, donde el agente ya tiene catálogo, planes analíticos, créditos y horas cargados.
/// </summary>
public class RecomendacionIAInternaDto
{
    public int SolicitudId { get; set; }
    public bool Disponible { get; set; }
    public string Motivo { get; set; } = string.Empty;

    /// <summary>Huella de la memoria del agente usada en la recomendación.</summary>
    public string HuellaMemoria { get; set; } = string.Empty;

    public List<RecomendacionMateriaDto> Recomendaciones { get; set; } = new();
}

/// <summary>
/// Petición de ayuda del agente interno mientras se selecciona la carrera, antes de registrar
/// la solicitud. No necesita número de solicitud ni delegación.
/// </summary>
public class RecomendacionCarreraRequest
{
    public int CarreraOrigenId { get; set; }
    public int CarreraDestinoId { get; set; }

    /// <summary>
    /// Si se envían ids, la recomendación se limita a esas materias de origen; si no, se usan
    /// todas las asignaturas del catálogo de la carrera origen.
    /// </summary>
    public List<int> AsignaturasOrigenIds { get; set; } = new();
}
