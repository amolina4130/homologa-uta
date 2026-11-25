using System.ComponentModel.DataAnnotations;

namespace HomologaUTA.Homologacion.DTOs;

/// <summary>Datos de la solicitud que vienen del servicio de Solicitudes.</summary>
public class SolicitudInfoDto
{
    public int Id { get; set; }
    public string NumeroProceso { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    /// <summary>Correo de la cuenta que registra: siempre el Comité, nunca el estudiante.</summary>
    public string EstudianteCorreo { get; set; } = string.Empty;

    /// <summary>Nombre real del estudiante, usado en los informes.</summary>
    public string EstudianteNombre { get; set; } = string.Empty;

    /// <summary>Cédula real del estudiante, usada en los informes.</summary>
    public string EstudianteCedula { get; set; } = string.Empty;

    public DateTime FechaRegistro { get; set; }
    public int CarreraOrigenId { get; set; }
    public int CarreraDestinoId { get; set; }
    public string CarreraOrigen { get; set; } = string.Empty;
    public string CarreraDestino { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;

    /// <summary>INTERNO (agente UTA) o EXTERNO (agente de otra universidad).</summary>
    public string Modulo { get; set; } = "INTERNO";

    public string? UniversidadOrigen { get; set; }
    public string? UniversidadDestino { get; set; }

    /// <summary>Número de asignaturas que el estudiante pidió homologar.</summary>
    public int TotalAsignaturas { get; set; }
}

/// <summary>Solicitud completa con las asignaturas de origen y su equivalente en destino.</summary>
public class SolicitudCompletaDto : SolicitudInfoDto
{
    public List<AsignaturaSolicitudDto> Asignaturas { get; set; } = new();
}

public class AsignaturaSolicitudDto
{
    public int SolicitudAsignaturaId { get; set; }

    public int AsignaturaOrigenId { get; set; }
    public string AsignaturaOrigen { get; set; } = string.Empty;
    public string NivelOrigen { get; set; } = string.Empty;
    public string AreaOrigen { get; set; } = string.Empty;
    public int CreditosOrigen { get; set; }
    public int HorasOrigen { get; set; }
    public string ContenidoOrigen { get; set; } = string.Empty;

    public int? AsignaturaDestinoId { get; set; }
    public string AsignaturaDestino { get; set; } = string.Empty;
    public string NivelDestino { get; set; } = string.Empty;
    public string AreaDestino { get; set; } = string.Empty;
    public int CreditosDestino { get; set; }
    public int HorasDestino { get; set; }
    public string ContenidoDestino { get; set; } = string.Empty;

    /// <summary>Equivalencia registrada en el catálogo, si se aplicó (HU-14).</summary>
    public string? EquivalenciaAplicada { get; set; }
}

/// <summary>Fila de la lista de solicitudes pendientes de revisión.</summary>
public class PendienteDto
{
    public int SolicitudId { get; set; }

    /// <summary>Número de proceso visible, por ejemplo SOL-0004 (HU-16).</summary>
    public string NumeroProceso { get; set; } = string.Empty;

    /// <summary>Fecha en que el Comité registró la solicitud.</summary>
    public DateTime Fecha { get; set; }

    /// <summary>Nombre real del estudiante (lo que debe verse en la columna Estudiante).</summary>
    public string Estudiante { get; set; } = string.Empty;
    public string EstudianteCedula { get; set; } = string.Empty;

    /// <summary>Correo del Comité que registró la solicitud.</summary>
    public string ComiteCorreo { get; set; } = string.Empty;

    /// <summary>INTERNO o EXTERNO, para hacer visible la modalidad en la bandeja.</summary>
    public string Modulo { get; set; } = "INTERNO";
    public string? UniversidadOrigen { get; set; }
    public string? UniversidadDestino { get; set; }

    public string Tipo { get; set; } = string.Empty;
    public string CarreraOrigen { get; set; } = string.Empty;
    public string CarreraDestino { get; set; } = string.Empty;

    /// <summary>Fecha en que terminó el análisis del agente.</summary>
    public DateTime FechaAnalisis { get; set; }

    public int TotalAsignaturas { get; set; }

    /// <summary>PENDIENTE_REVISION o INFORME_EMITIDO (HU-17).</summary>
    public string Estado { get; set; } = string.Empty;
}

/// <summary>Detalle de una solicitud con la propuesta del agente por asignatura.</summary>
public class DetalleSolicitudDto
{
    public int SolicitudId { get; set; }

    /// <summary>Número de proceso visible, por ejemplo SOL-0004 (HU-16).</summary>
    public string NumeroProceso { get; set; } = string.Empty;

    /// <summary>PENDIENTE_REVISION o INFORME_EMITIDO.</summary>
    public string Estado { get; set; } = string.Empty;

    public DateTime Fecha { get; set; }

    /// <summary>Nombre real del estudiante al que pertenece la homologación.</summary>
    public string Estudiante { get; set; } = string.Empty;

    public string EstudianteCedula { get; set; } = string.Empty;

    /// <summary>Correo del Comité que registró la solicitud (auditoría).</summary>
    public string ComiteCorreo { get; set; } = string.Empty;

    public string Tipo { get; set; } = string.Empty;
    public string CarreraOrigen { get; set; } = string.Empty;
    public string CarreraDestino { get; set; } = string.Empty;
    public string Modulo { get; set; } = "INTERNO";
    public string? UniversidadOrigen { get; set; }
    public string? UniversidadDestino { get; set; }
    public DateTime? FechaAnalisis { get; set; }

    /// <summary>Solo si el informe ya se emitió.</summary>
    public string? CoordinadorCorreo { get; set; }
    public DateTime? FechaInforme { get; set; }

    /// <summary>Resolución del Consejo Directivo, si ya se registró (HU-17).</summary>
    public ResolucionDto? Resolucion { get; set; }

    public List<DetalleAsignaturaDto> Asignaturas { get; set; } = new();
}

public class DetalleAsignaturaDto
{
    /// <summary>Id de la asignatura analizada; se usa al emitir el informe.</summary>
    public int Id { get; set; }

    public string AsignaturaOrigen { get; set; } = string.Empty;
    public string AsignaturaDestino { get; set; } = string.Empty;

    /// <summary>Id de la asignatura de destino en el catálogo (HU-21).</summary>
    public int? AsignaturaDestinoId { get; set; }

    public string? AreaOrigen { get; set; }
    public string? AreaDestino { get; set; }

    /// <summary>true cuando la asignatura se cubre con dos materias de origen (HU-22).</summary>
    public bool Combinada { get; set; }
    public List<MateriaOrigenDto> MateriasOrigen { get; set; } = new();

    public PropuestaAgenteDto Propuesta { get; set; } = new();

    /// <summary>Decisión y observación del Coordinador; null mientras no emita el informe.</summary>
    public string? DecisionCoordinador { get; set; }
    public string? ObservacionCoordinador { get; set; }

    /// <summary>Decisión final del Consejo Directivo, si ya se registró la resolución (HU-17).</summary>
    public string? DecisionFinal { get; set; }

    /// <summary>true cuando el Coordinador decidió distinto que el Docente.</summary>
    public bool CambioRespectoDocente { get; set; }

    // Datos de la comparación que vio la IA (semestres de las mallas, horas, créditos,
    // coincidencias, diferencias y regla aplicada).
    public string? NivelOrigen { get; set; }
    public string? NivelDestino { get; set; }
    public int? HorasOrigen { get; set; }
    public int? HorasDestino { get; set; }
    public int? CreditosOrigen { get; set; }
    public int? CreditosDestino { get; set; }
    public string? ContenidosCoincidentes { get; set; }
    public string? Diferencias { get; set; }
    public string? ReglaAplicada { get; set; }

    /// <summary>IA o DOCENTE: quién propuso esta fila.</summary>
    public string PropuestaPor { get; set; } = "IA";
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

    /// <summary>
    /// true si el servicio de Solicitudes confirmó el cambio a INFORME_EMITIDO.
    /// El informe queda guardado aunque este valor sea false.
    /// </summary>
    public bool EstadoSolicitudActualizado { get; set; }

    public List<InformeAsignaturaResultadoDto> Asignaturas { get; set; } = new();

    // Datos del informe final dirigido al Consejo Directivo (opcionales en el informe técnico).
    public string? InformeFinalTexto { get; set; }
    public string? NombreEstudiante { get; set; }
    public string? CedulaEstudiante { get; set; }
    public string? NumeroMemorando { get; set; }
    public string? NombreFirmante { get; set; }
}

public class InformeAsignaturaResultadoDto
{
    public int AsignaturaId { get; set; }
    public string AsignaturaOrigen { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string Observacion { get; set; } = string.Empty;
}