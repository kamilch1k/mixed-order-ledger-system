package processor

import (
	"encoding/json"
	"os"
	"path/filepath"
	"testing"
	"time"

	"github.com/kamilch1k/mixed-order-ledger-system/worker/internal/contracts"
)

func TestProcessOnceCompletesOrderAndWritesLedger(t *testing.T) {
	dataPath := t.TempDir()
	processor := New(dataPath)
	fixedNow := time.Date(2026, 6, 17, 12, 0, 0, 0, time.UTC)
	processor.Now = func() time.Time { return fixedNow }

	orderID := "11111111-1111-1111-1111-111111111111"
	order := contracts.Order{
		ID:          orderID,
		CustomerID:  "customer-123",
		Status:      "Pending",
		Currency:    "USD",
		TotalAmount: 55.50,
		Items: []contracts.OrderItem{
			{SKU: "SKU-1", Quantity: 1, UnitPrice: 55.50, LineTotal: 55.50},
		},
		CreatedAtUTC: fixedNow.Add(-time.Hour),
		UpdatedAtUTC: fixedNow.Add(-time.Hour),
	}
	event := contracts.OutboxEvent{
		ID:            "22222222-2222-2222-2222-222222222222",
		Type:          "OrderCreated",
		OrderID:       orderID,
		OccurredAtUTC: fixedNow.Add(-time.Minute),
		SchemaVersion: "v1",
	}

	writeFixture(t, filepath.Join(dataPath, "orders", "11111111111111111111111111111111.json"), order)
	writeFixture(t, filepath.Join(dataPath, "outbox", "22222222222222222222222222222222.json"), event)

	result, err := processor.ProcessOnce()
	if err != nil {
		t.Fatalf("process once: %v", err)
	}
	if result.Processed != 1 {
		t.Fatalf("expected one processed event, got %d", result.Processed)
	}

	var updated contracts.Order
	readFixture(t, filepath.Join(dataPath, "orders", "11111111111111111111111111111111.json"), &updated)
	if updated.Status != "Completed" {
		t.Fatalf("expected completed order, got %q", updated.Status)
	}

	entries, err := filepath.Glob(filepath.Join(dataPath, "ledger", "*.json"))
	if err != nil {
		t.Fatalf("glob ledger: %v", err)
	}
	if len(entries) != 2 {
		t.Fatalf("expected two ledger entries, got %d", len(entries))
	}

	result, err = processor.ProcessOnce()
	if err != nil {
		t.Fatalf("second process once: %v", err)
	}
	if result.Processed != 0 {
		t.Fatalf("expected idempotent second run, got %d processed", result.Processed)
	}
}

func writeFixture(t *testing.T, path string, value any) {
	t.Helper()
	if err := os.MkdirAll(filepath.Dir(path), 0o755); err != nil {
		t.Fatalf("mkdir: %v", err)
	}
	file, err := os.Create(path)
	if err != nil {
		t.Fatalf("create fixture: %v", err)
	}
	defer file.Close()
	if err := json.NewEncoder(file).Encode(value); err != nil {
		t.Fatalf("encode fixture: %v", err)
	}
}

func readFixture[T any](t *testing.T, path string, value *T) {
	t.Helper()
	file, err := os.Open(path)
	if err != nil {
		t.Fatalf("open fixture: %v", err)
	}
	defer file.Close()
	if err := json.NewDecoder(file).Decode(value); err != nil {
		t.Fatalf("decode fixture: %v", err)
	}
}
