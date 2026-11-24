namespace HomologaUTA.Solicitudes.Models;

public class Solicitud
{
    /// <summary>Valor usado hasta que el récord académico entregue el nombre real.</summary>
    public const string NombreSinIdentificar = "Estudiante sin identificar";

    /// <summary>Valor usado hasta que el récord académico entregue la cédula real.</summary>
    public const string CedulaSinIdentificar = "-";

    public int Id { get; set; }

    /// <summary>Número de proceso legible y único, por ejemplo SOL-0004 (HU-16).</summary>
    public string NumeroProceso { get; set; } = string.Empty;

    public string Tipo { get; set; } = string.Empty; 
    
    public int CarreraOrigenId { get; set; }
    public Carrera CarreraOrigen { get; set; } = null!;
    
    public int CarreraDestinoId { get; set; }
    public Carrera CarreraDestino { get; set; } = null!;
    
    /// <summary>Correo de la cuenta que registra (siempre el Comité), usado como auditoría.</summary>
    public string EstudianteCorreo { get; set; } = string.Empty;

    /// <summary>Nombre real del estudiante al que pertenece el trámite.</summary>
    public string EstudianteNombre { get; set; } = string.Empty;

    /// <summary>Cédula real del estudiante; se imprime en los informes.</summary>
    public string EstudianteCedula { get; set; } = string.Empty;

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public string Estado { get; set; } = EstadoSolicitud.REGISTRADA;

    /// <summary>INTERNO usa el agente de la UTA; EXTERNO usa el conector de universidades.</summary>
    public string Modulo { get; set; } = "INTERNO";

    public string? UniversidadOrigen { get; set; }
    public string? UniversidadDestino { get; set; }
    
    public List<SolicitudAsignatura> Asignaturas { get; set; } = new();
}