namespace OrderLedger.Api.Contracts;

public sealed record CreateOrderRequest(
    string CustomerId,
    IReadOnlyCollection<CreateOrderItemRequest> Items,
    string? Currency = "USD");

public sealed record CreateOrderItemRequest(
    string Sku,
    int Quantity,
    decimal UnitPrice);

public sealed record OrderResponse(
    Guid Id,
    string CustomerId,
    string Status,
    string Currency,
    decimal TotalAmount,
    IReadOnlyCollection<OrderItemResponse> Items,
    string? IdempotencyKey,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record OrderItemResponse(
    string Sku,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record OutboxEvent(
    Guid Id,
    string Type,
    Guid OrderId,
    DateTime OccurredAtUtc,
    string SchemaVersion);

public sealed record OutboxResponse(IReadOnlyCollection<OutboxEvent> Items);

public sealed record LedgerEntry(
    Guid Id,
    Guid OrderId,
    string Type,
    decimal Amount,
    string Currency,
    string Description,
    DateTime CreatedAtUtc);

public sealed record LedgerResponse(Guid OrderId, IReadOnlyCollection<LedgerEntry> Entries);
