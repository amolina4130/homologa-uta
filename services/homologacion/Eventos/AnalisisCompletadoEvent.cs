namespace HomologaUTA.Homologacion.Eventos;

/// <summary>
/// Evento que se publica en RabbitMQ cuando termina el análisis de una solicitud.
/// Mismo contrato que definió TE-01 en el servicio de Solicitudes.
/// </summary>
public class AnalisisCompletadoEvent
{
    public int SolicitudId { get; set; }

    /// <summary>Estado en el que queda la solicitud, por ejemplo PENDIENTE_REVISION.</summary>
    public string EstadoFinal { get; set; } = string.Empty;

    public DateTime FechaAnalisis { get; set; } = DateTime.UtcNow;
}