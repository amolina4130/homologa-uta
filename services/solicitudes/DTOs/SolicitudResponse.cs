namespace HomologaUTA.Solicitudes.DTOs;

public class SolicitudResponse
{
    public int Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string CarreraOrigen { get; set; } = string.Empty;
    public string CarreraDestino { get; set; } = string.Empty;
    public string FechaRegistro { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
}