using System.Text.Json;
using OrderFlow.Worker.Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrderFlow.Worker;

public sealed class Worker(
    ILogger<Worker> logger,
    IConfiguration configuration
) : BackgroundService
{
    private const string NomeFila = "pedidos.criados";

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken
    )
    {
        logger.LogInformation(
        ">>> ExecuteAsync do OrderFlow.Worker foi iniciado!");

        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:Host"] ?? "localhost",
            VirtualHost = configuration["RabbitMQ:VirtualHost"] ?? "orderflow",
            UserName = configuration["RabbitMQ:UserName"] ?? throw new InvalidOperationException("Configure RabbitMQ:UserName"),
            Password = configuration["RabbitMQ:Password"] ?? throw new InvalidOperationException("Configure RabbitMQ:Password"),
            ClientProvidedName = "orderflow-worker"
        };

        logger.LogInformation(
            "Conectando ao RabbitMQ: {Host}:{Port}, VHost: {VHost}, Usuario: {Usuario}",
            factory.HostName,
            factory.Port,
            factory.VirtualHost,
            factory.UserName
        );

        await using var connection =
            await factory.CreateConnectionAsync(stoppingToken);

        logger.LogInformation(">>> Conexão RabbitMQ estabelecida!");

        await using var channel =
            await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        logger.LogInformation(">>> Canal RabbitMQ criado!");

        await channel.QueueDeclareAsync(
            queue: NomeFila,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken
        );

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: stoppingToken
        );

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            PedidoCriado pedido;

            try
            {
                pedido = JsonSerializer.Deserialize<PedidoCriado>(
                    ea.Body.Span
                ) ?? throw new JsonException("Evento vazio");

                if (pedido.Versao != 1 ||
                    pedido.PedidoId == Guid.Empty ||
                    string.IsNullOrWhiteSpace(pedido.ClienteId) ||
                    pedido.Itens is null ||
                    pedido.Itens.Count == 0)
                {
                    throw new JsonException("Contrato do evento inválido");
                }
            }
            catch (JsonException ex)
            {
                logger.LogError(
                    ex,
                    "Mensagem inválida recebida na fila {Fila}",
                    NomeFila
                );

                await channel.BasicNackAsync(
                    deliveryTag: ea.DeliveryTag,
                    multiple: false,
                    requeue: false,
                    cancellationToken: stoppingToken
                );

                return;
            }

            logger.LogInformation(
                "Processando pedido {PedidoId} do cliente {ClienteId}",
                pedido.PedidoId,
                pedido.ClienteId
            );

            logger.LogInformation(
                "Pedido {PedidoId}: {Quantidade} itens recebidos",
                pedido.PedidoId,
                pedido.Itens.Count
            );

            await channel.BasicAckAsync(
                deliveryTag: ea.DeliveryTag,
                multiple: false,
                cancellationToken: stoppingToken
            );

            logger.LogInformation(
                "Pedido {PedidoId} confirmado com ACK",
                pedido.PedidoId
            );
        };

        await channel.BasicConsumeAsync(
                queue: NomeFila,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken
            );

        logger.LogInformation(
            "Worker iniciado. Aguardando mensagens na fila {Fila}",
            NomeFila
        );

        try
        {
            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                stoppingToken
            );
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Worker encerrado");
        }
    }
}