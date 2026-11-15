namespace HomologaUTA.Solicitudes.Models;

public class Solicitud
{
    public int Id { get; set; }

    /// <summary>Número de proceso legible y único, por ejemplo SOL-0004 (HU-16).</summary>
    public string NumeroProceso { get; set; } = string.Empty;

    public string Tipo { get; set; } = string.Empty;

    /// <summary>INTERNO (carreras del catálogo) o EXTERNO (universidades escritas por el estudiante).</summary>
    public string Modulo { get; set; } = "INTERNO";

    public int CarreraOrigenId { get; set; }
    public Carrera CarreraOrigen { get; set; } = null!;

    public int CarreraDestinoId { get; set; }
    public Carrera CarreraDestino { get; set; } = null!;

    /// <summary>Universidad de origen escrita por el estudiante (solo módulo externo).</summary>
    public string? UniversidadOrigen { get; set; }

    /// <summary>Universidad de destino escrita por el estudiante (solo módulo externo).</summary>
    public string? UniversidadDestino { get; set; }

    public string EstudianteCorreo { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public string Estado { get; set; } = EstadoSolicitud.REGISTRADA;

    public List<SolicitudAsignatura> Asignaturas { get; set; } = new();
}
