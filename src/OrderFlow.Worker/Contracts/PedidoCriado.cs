namespace OrderFlow.Worker.Contracts;

public sealed record PedidoCriado(
    Guid EventoId,
    Guid PedidoId,
    DateTime OcorridoEmUtc,
    int Versao,
    string ClienteId,
    List<ItemPedido> Itens
);

public sealed record ItemPedido(
    string ProdutoId,
    int Quantidade,
    decimal PrecoUnitario
);