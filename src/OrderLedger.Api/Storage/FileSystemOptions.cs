namespace OrderLedger.Api.Storage;

public sealed record FileSystemOptions(string DataPath)
{
    public string OrdersPath => Path.Combine(DataPath, "orders");
    public string OutboxPath => Path.Combine(DataPath, "outbox");
    public string LedgerPath => Path.Combine(DataPath, "ledger");
    public string IdempotencyPath => Path.Combine(DataPath, "idempotency");
}
