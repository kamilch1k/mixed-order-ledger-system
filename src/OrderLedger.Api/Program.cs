using OrderLedger.Api.Contracts;
using OrderLedger.Api.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<FileSystemOptions>(_ =>
{
    var dataPath = builder.Configuration.GetValue<string>("Storage:DataPath") ?? "data";
    return new FileSystemOptions(Path.GetFullPath(dataPath));
});
builder.Services.AddSingleton<OrderStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/orders", async (CreateOrderRequest request, HttpRequest httpRequest, OrderStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.CustomerId) || request.Items.Count == 0)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["order"] = ["customerId and at least one item are required."]
        });
    }

    if (request.Items.Any(item => string.IsNullOrWhiteSpace(item.Sku) || item.Quantity <= 0 || item.UnitPrice <= 0))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["items"] = ["each item requires sku, positive quantity, and positive unitPrice."]
        });
    }

    var idempotencyKey = httpRequest.Headers.TryGetValue("Idempotency-Key", out var values)
        ? values.ToString()
        : null;
    var order = await store.CreateOrderAsync(request, idempotencyKey);
    return Results.Created($"/api/orders/{order.Id}", order);
});

app.MapGet("/api/orders/{id:guid}", async (Guid id, OrderStore store) =>
{
    var order = await store.GetOrderAsync(id);
    return order is null ? Results.NotFound() : Results.Ok(order);
});

app.MapGet("/api/orders/{id:guid}/ledger", async (Guid id, OrderStore store) =>
{
    var entries = await store.GetLedgerEntriesAsync(id);
    return Results.Ok(new LedgerResponse(id, entries));
});

app.MapGet("/api/outbox", async (OrderStore store) => Results.Ok(new OutboxResponse(await store.GetPendingEventsAsync())));

await app.RunAsync();

public partial class Program;
