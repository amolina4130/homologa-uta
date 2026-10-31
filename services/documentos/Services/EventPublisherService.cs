using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace HomologaUTA.Documentos.Services;

public class EventPublisherService
{
    private readonly string _hostname;

    public EventPublisherService(IConfiguration config)
    {
        _hostname = config["RabbitMQ:Host"] ?? "rabbitmq";
    }

    public async Task PublicarDocumentoProcesadoAsync(int documentoId, int solicitudId, string textoExtraido)
    {
        var factory = new ConnectionFactory() { HostName = _hostname };
        
        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(
            queue: "documento_procesado", 
            durable: true, 
            exclusive: false, 
            autoDelete: false);

        var evento = new
        {
            DocumentoId = documentoId,
            SolicitudId = solicitudId,
            TextoExtraido = textoExtraido.Substring(0, Math.Min(100, textoExtraido.Length)) + "...",
            FechaProcesamiento = DateTime.UtcNow
        };

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evento));

        await channel.BasicPublishAsync(
            exchange: "",
            routingKey: "documento_procesado",
            body: body);
    }
}