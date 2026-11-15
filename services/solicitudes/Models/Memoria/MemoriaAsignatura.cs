namespace HomologaUTA.Solicitudes.Models.Memoria;

/// <summary>
/// Memoria del agente (HU-19): una asignatura del catálogo con su nivel, área, créditos, horas y
/// contenidos mínimos. Se llena con una carga única desde las mallas y los planes analíticos.
/// </summary>
public class MemoriaAsignatura
{
    public int Id { get; set; }

    public int CarreraId { get; set; }
    public int AsignaturaId { get; set; }

    public string Nombre { get; set; } = string.Empty;

    /// <summary>Nivel numérico (1 = Primero, 2 = Segundo, ...).</summary>
    public int Nivel { get; set; }

    /// <summary>Área usada por la preselección (Física, Programación, Matemáticas, ...).</summary>
    public string Area { get; set; } = "General";

    public int Creditos { get; set; }
    public int Horas { get; set; }
    public string ContenidosMinimos { get; set; } = string.Empty;
}

/// <summary>Estado de la memoria: huella, fecha de la última carga y totales.</summary>
public class MemoriaEstado
{
    public int Id { get; set; }
    public string Huella { get; set; } = string.Empty;
    public DateTime FechaCarga { get; set; } = DateTime.UtcNow;
    public int TotalCarreras { get; set; }
    public int TotalAsignaturas { get; set; }
}
