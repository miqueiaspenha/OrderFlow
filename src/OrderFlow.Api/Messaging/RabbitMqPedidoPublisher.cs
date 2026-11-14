using System.Text.Json;
using OrderFlow.Api.Contracts;
using RabbitMQ.Client;

namespace OrderFlow.Api.Messaging;

public sealed class RabbitMqPedidoPublisher(
    IConnection connection)
{
    private const string NomeFila = "pedidos.criados";

    public async Task PublicarAsync(
        PedidoCriado evento,
        CancellationToken cancellationToken = default)
    {
        var options = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true
        );

        await using var channel =
            await connection.CreateChannelAsync(
                options, cancellationToken
            );

        await channel.QueueDeclareAsync(
            queue: NomeFila,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken
        );

        var mensagem = JsonSerializer.SerializeToUtf8Bytes(evento);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            Persistent = true,
            MessageId = evento.EventoId.ToString()
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: NomeFila,
            mandatory: true,
            basicProperties: properties,
            body: mensagem,
            cancellationToken: cancellationToken
        );
    }
}