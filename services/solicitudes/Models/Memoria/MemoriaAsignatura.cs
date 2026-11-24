namespace HomologaUTA.Solicitudes.Models.Memoria;

/// <summary>
/// Memoria relacional del agente (HU-19): una fila por asignatura de la malla de destino,
/// con sus créditos, horas, área y contenidos mínimos.
/// </summary>
public class MemoriaAsignatura
{
    public int Id { get; set; }

    public int CarreraId { get; set; }
    public string Carrera { get; set; } = string.Empty;

    public int AsignaturaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Nivel { get; set; } = string.Empty;
    public int NivelNumerico { get; set; }
    public string Area { get; set; } = string.Empty;
    public int Creditos { get; set; }
    public int Horas { get; set; }
    public string ContenidosMinimos { get; set; } = string.Empty;

    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;
}

/// <summary>Huella y fecha de la última carga de la memoria del agente.</summary>
public class MemoriaEstado
{
    public int Id { get; set; }
    public string Huella { get; set; } = string.Empty;
    public DateTime FechaCarga { get; set; } = DateTime.UtcNow;
    public int TotalCarreras { get; set; }
    public int TotalAsignaturas { get; set; }
}
