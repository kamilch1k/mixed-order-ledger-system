param(
    [string]$BaseUrl = "http://localhost:5207",
    [string]$WorkerPath = ".\worker\bin\ledger-worker.exe",
    [string]$DataPath = ".\data"
)

$ErrorActionPreference = "Stop"

function Wait-ForApi {
    param([string]$Url)

    $deadline = (Get-Date).AddSeconds(30)
    do {
        try {
            Invoke-RestMethod "$Url/health" | Out-Null
            return
        }
        catch {
            Start-Sleep -Seconds 1
        }
    } while ((Get-Date) -lt $deadline)

    throw "API did not become healthy at $Url/health"
}

if (-not (Test-Path $WorkerPath)) {
    throw "Worker executable not found at $WorkerPath. Build it with: cd worker; go build -o bin/ledger-worker.exe ./cmd/ledger-worker"
}

Wait-ForApi $BaseUrl

$order = Invoke-RestMethod "$BaseUrl/api/orders" `
    -Method Post `
    -Headers @{ "Idempotency-Key" = "verify-$([Guid]::NewGuid().ToString('N'))" } `
    -ContentType "application/json" `
    -Body (@{
        customerId = "customer-verification"
        currency = "USD"
        items = @(
            @{
                sku = "SKU-MIXED-BACKEND"
                quantity = 2
                unitPrice = 49.50
            }
        )
    } | ConvertTo-Json -Depth 10)

$env:DATA_PATH = (Resolve-Path $DataPath).Path
$workerOutput = & $WorkerPath
if ($LASTEXITCODE -ne 0) {
    throw "Worker failed: $workerOutput"
}

$updated = Invoke-RestMethod "$BaseUrl/api/orders/$($order.id)"
$ledger = Invoke-RestMethod "$BaseUrl/api/orders/$($order.id)/ledger"

[PSCustomObject]@{
    Health = "OK"
    OrderId = $order.id
    OrderStatus = $updated.status
    LedgerEntryCount = $ledger.entries.Count
    WorkerOutput = ($workerOutput | Out-String).Trim()
}
