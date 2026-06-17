# Local Testing Guide

This guide verifies the mixed .NET + Go backend with fictional order data.

## Start API

```powershell
dotnet run --project src/OrderLedger.Api/OrderLedger.Api.csproj --urls http://localhost:5207
```

## Build Worker

```powershell
cd worker
go build -o bin/ledger-worker.exe ./cmd/ledger-worker
cd ..
```

## Verify End-to-End

```powershell
.\scripts\verify-local.ps1
```

Expected result:

```text
Health: OK
OrderStatus: Completed
LedgerEntryCount: 2
```

## Reset Local Data

Stop the API and delete the local data folder:

```powershell
Remove-Item .\data -Recurse -Force -ErrorAction SilentlyContinue
```
