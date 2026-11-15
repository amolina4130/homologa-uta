namespace HomologaUTA.Solicitudes.Models;

/// <summary>
/// Equivalencia registrada por el Administrador entre una asignatura de origen y una de destino (HU-14).
/// Cuando está activa, el análisis la cita como regla aplicada antes de llamar al agente.
/// </summary>
public class Equivalencia
{
    public int Id { get; set; }

    public int CarreraOrigenId { get; set; }
    public Carrera CarreraOrigen { get; set; } = null!;

    public int AsignaturaOrigenId { get; set; }
    public Asignatura AsignaturaOrigen { get; set; } = null!;

    public int CarreraDestinoId { get; set; }
    public Carrera CarreraDestino { get; set; } = null!;

    public int AsignaturaDestinoId { get; set; }
    public Asignatura AsignaturaDestino { get; set; } = null!;

    public bool Activa { get; set; } = true;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
