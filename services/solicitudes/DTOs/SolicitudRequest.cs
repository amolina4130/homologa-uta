namespace HomologaUTA.Solicitudes.DTOs;

public class SolicitudRequest
{
    // public string Tipo { get; set; } = string.Empty; // Ya no se usa
    public int CarreraOrigenId { get; set; }
    public int CarreraDestinoId { get; set; }
    public List<int> AsignaturasIds { get; set; } = new();
}