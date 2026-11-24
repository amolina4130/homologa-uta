namespace HomologaUTA.Solicitudes.DTOs;

/// <summary>Nombre y cédula reales del titular, corregidos por el gestor del trámite.</summary>
public class ActualizarTitularRequest
{
    public string? Nombre { get; set; }
    public string? Cedula { get; set; }
}
