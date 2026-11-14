namespace OrderFlow.Api.Contracts;

public sealed record CriarPedidoRequest(
    string ClienteId,
    List<ItemPedidoRequest> Itens
);

public sealed record ItemPedidoRequest(
    string ProdutoId,
    int Quantidade,
    decimal PrecoUnitario
);