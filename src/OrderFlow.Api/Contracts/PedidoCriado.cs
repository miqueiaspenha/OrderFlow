using OrderFlow.Api.Contracts;

namespace  OrderFlow.Api.Contracts;

public sealed record PedidoCriado(
    Guid EventoId,
    Guid PedidoId,
    DateTime OcorridoEmUtc,
    int Versao,
    string ClienteId,
    List<ItemPedidoRequest> Itens
);