using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OrderLedger.Api.Contracts;

namespace OrderLedger.Api.Storage;

public sealed class OrderStore
{
    private readonly FileSystemOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public OrderStore(FileSystemOptions options)
    {
        _options = options;
        Directory.CreateDirectory(_options.OrdersPath);
        Directory.CreateDirectory(_options.OutboxPath);
        Directory.CreateDirectory(_options.LedgerPath);
        Directory.CreateDirectory(_options.IdempotencyPath);
    }

    public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, string? idempotencyKey)
    {
        await _gate.WaitAsync();
        try
        {
            idempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
            if (idempotencyKey is not null)
            {
                var existingId = await ReadIdempotencyIndexAsync(idempotencyKey);
                if (existingId.HasValue)
                {
                    var existing = await GetOrderAsync(existingId.Value);
                    if (existing is not null)
                    {
                        return existing;
                    }
                }
            }

            var now = DateTime.UtcNow;
            var items = request.Items.Select(item => new OrderItemResponse(
                item.Sku.Trim(),
                item.Quantity,
                item.UnitPrice,
                item.Quantity * item.UnitPrice)).ToList();

            var order = new OrderResponse(
                Guid.NewGuid(),
                request.CustomerId.Trim(),
                "Pending",
                string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.Trim().ToUpperInvariant(),
                items.Sum(item => item.LineTotal),
                items,
                idempotencyKey,
                now,
                now);

            await WriteJsonAsync(OrderPath(order.Id), order);
            if (idempotencyKey is not null)
            {
                await File.WriteAllTextAsync(IdempotencyPath(idempotencyKey), order.Id.ToString());
            }

            var outboxEvent = new OutboxEvent(Guid.NewGuid(), "OrderCreated", order.Id, now, "v1");
            await WriteJsonAsync(OutboxPath(outboxEvent.Id), outboxEvent);

            return order;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OrderResponse?> GetOrderAsync(Guid id)
    {
        var path = OrderPath(id);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<OrderResponse>(stream, _jsonOptions);
    }

    public async Task<IReadOnlyCollection<LedgerEntry>> GetLedgerEntriesAsync(Guid orderId)
    {
        var pattern = $"{orderId:N}-*.json";
        var entries = new List<LedgerEntry>();
        foreach (var path in Directory.GetFiles(_options.LedgerPath, pattern))
        {
            await using var stream = File.OpenRead(path);
            var entry = await JsonSerializer.DeserializeAsync<LedgerEntry>(stream, _jsonOptions);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        return entries.OrderBy(entry => entry.CreatedAtUtc).ToList();
    }

    public async Task<IReadOnlyCollection<OutboxEvent>> GetPendingEventsAsync()
    {
        var events = new List<OutboxEvent>();
        foreach (var path in Directory.GetFiles(_options.OutboxPath, "*.json"))
        {
            await using var stream = File.OpenRead(path);
            var item = await JsonSerializer.DeserializeAsync<OutboxEvent>(stream, _jsonOptions);
            if (item is not null)
            {
                events.Add(item);
            }
        }

        return events.OrderBy(item => item.OccurredAtUtc).ToList();
    }

    private async Task<Guid?> ReadIdempotencyIndexAsync(string key)
    {
        var path = IdempotencyPath(key);
        if (!File.Exists(path))
        {
            return null;
        }

        var value = await File.ReadAllTextAsync(path);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private string OrderPath(Guid id) => Path.Combine(_options.OrdersPath, $"{id:N}.json");

    private string OutboxPath(Guid id) => Path.Combine(_options.OutboxPath, $"{id:N}.json");

    private string IdempotencyPath(string key)
    {
        var safe = Convert.ToHexString(Encoding.UTF8.GetBytes(key));
        return Path.Combine(_options.IdempotencyPath, $"{safe}.txt");
    }

    private async Task WriteJsonAsync<T>(string path, T value)
    {
        var tempPath = $"{path}.tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, value, _jsonOptions);
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        File.Move(tempPath, path);
    }
}
