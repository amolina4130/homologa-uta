namespace HomologaUTA.Homologacion.Clients;

public class MemoriaEstadoDto
{
    public string Huella { get; set; } = string.Empty;
    public DateTime FechaCarga { get; set; }
    public int TotalCarreras { get; set; }
    public int TotalAsignaturas { get; set; }
}

public interface IMemoriaClient
{
    // Devuelve el estado de la memoria o null si no está cargada (404)
    Task<MemoriaEstadoDto?> ObtenerEstadoMemoriaAsync(CancellationToken ct = default);
}