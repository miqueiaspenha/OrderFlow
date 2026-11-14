using OrderFlow.Api.Contracts;
using OrderFlow.Api.Messaging;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var factory = new ConnectionFactory
{
    HostName = builder.Configuration["RabbitMQ:Host"] ?? "lab-rabbitmq",
    VirtualHost = builder.Configuration["RabbitMQ:VirtualHost"] ?? "orderflow",
    UserName = builder.Configuration["RabbitMQ:UserName"] ?? throw new InvalidOperationException("Configure RabbitMQ:UserName"),
    Password = builder.Configuration["RabbitMQ:Password"] ?? throw new InvalidOperationException("Configure RabbitMQ:Password"),
    ClientProvidedName = "orderflow-api"
};

await using var connection =
    await factory.CreateConnectionAsync();

builder.Services.AddSingleton<IConnection>(connection);
builder.Services.AddSingleton<RabbitMqPedidoPublisher>();

var app = builder.Build();

if(app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapPost("/pedidos", async (
    CriarPedidoRequest request,
    RabbitMqPedidoPublisher publisher,
    ILogger<Program> logger,
    CancellationToken cancellationToken ) =>
{
    var erros = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(request.ClienteId))
    {
        erros["clienteId"] = ["O cliente é obrigatório."];
    }

    if(request.Itens is null || request.Itens.Count == 0)
    {
        erros["itens"] = ["Informe pelo menos um item."];
    }
    else if (request.Itens.Any(item =>
        string.IsNullOrWhiteSpace(item.ProdutoId) ||
        item.Quantidade <= 0 ||
        item.PrecoUnitario <= 0))
    {
        erros["itens"] = ["O pedido contém itens inválidos."];
    }

    if (erros.Count > 0)
    {
        return Results.ValidationProblem(erros);
    }

    var evento = new PedidoCriado(
        EventoId: Guid.NewGuid(),
        PedidoId: Guid.NewGuid(),
        OcorridoEmUtc: DateTime.UtcNow,
        Versao: 1,
        ClienteId: request.ClienteId,
        Itens: request.Itens!
    );

    try
    {
        await publisher.PublicarAsync(evento, cancellationToken);
    }
    catch(Exception ex) when (ex is not OperationCanceledException)
    {
        logger.LogError(ex, "Falha ao publicar o pedido {PedidoId}",
        evento.PedidoId);

        return Results.Problem(
            title: "Não foi possível confirmar a publicação.",
            statusCode: 503
        );
    }

    return Results.Accepted(value: new
    {
        evento.PedidoId,
        Status = "Enfileirado"
    });
})
.WithName("CriarPedido")
.WithTags("Pedidos");

app.Run();