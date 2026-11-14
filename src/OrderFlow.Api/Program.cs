using OrderFlow.Api.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if(app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapPost("/pedidos", (CriarPedidoRequest request) =>
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

    var pedidoId = Guid.NewGuid();

    return Results.Ok(new
    {
        PedidoId = pedidoId,
        Status = "Validado"
    });
})
.WithName("CriarPedido")
.WithTags("Pedidos");

app.Run();