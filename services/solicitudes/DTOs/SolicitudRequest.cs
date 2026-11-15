namespace HomologaUTA.Solicitudes.DTOs;

public class SolicitudRequest
{
    // public string Tipo { get; set; } = string.Empty; // Ya no se usa
    public int CarreraOrigenId { get; set; }
    public int CarreraDestinoId { get; set; }
    public List<int> AsignaturasIds { get; set; } = new();

    /// <summary>Módulo de análisis: INTERNO (agente UTA) o EXTERNO (agente de otra universidad).</summary>
    public string Modulo { get; set; } = "INTERNO";

    /// <summary>Universidad de origen cuando el módulo es EXTERNO.</summary>
    public string? UniversidadOrigen { get; set; }

    /// <summary>Universidad de destino cuando el módulo es EXTERNO.</summary>
    public string? UniversidadDestino { get; set; }
}