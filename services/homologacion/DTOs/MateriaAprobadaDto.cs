namespace HomologaUTA.Homologacion.Models;

public class MateriaAprobada
{
    public int Id { get; set; }
    public int SolicitudId { get; set; }          // AJUSTAR: tipo igual al Id de Solicitud
    public string Nombre { get; set; } = string.Empty;
    public decimal? Nota { get; set; }
    public string EstadoSegunDocumento { get; set; } = string.Empty;
    public string? Periodo { get; set; }
}