using System.Text;
using System.Text.Json;
using HomologaUTA.Homologacion.Eventos;
using RabbitMQ.Client;

namespace HomologaUTA.Homologacion.Services;

public interface IAnalisisEventPublisher
{
    Task PublicarAnalisisCompletadoAsync(AnalisisCompletadoEvent evento);
}

/// <summary>Publica el evento AnalisisCompletado en la cola "analisis_completado" de RabbitMQ.</summary>
public class AnalisisEventPublisher : IAnalisisEventPublisher
{
    public const string NombreCola = "analisis_completado";

    private readonly string _hostname;

    public AnalisisEventPublisher(IConfiguration config)
    {
        // Igual que el servicio de documentos: "rabbitmq" es el nombre del contenedor en el compose
        _hostname = config["RabbitMQ:Host"] ?? "rabbitmq";
    }

    public async Task PublicarAnalisisCompletadoAsync(AnalisisCompletadoEvent evento)
    {
        var factory = new ConnectionFactory() { HostName = _hostname };

        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(
            queue: NombreCola,
            durable: true,
            exclusive: false,
            autoDelete: false);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evento));

        await channel.BasicPublishAsync(
            exchange: "",
            routingKey: NombreCola,
            body: body);
    }
}