package processor

import (
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"strings"
	"time"

	"github.com/kamilch1k/mixed-order-ledger-system/worker/internal/contracts"
)

type Processor struct {
	DataPath string
	Now      func() time.Time
}

type Result struct {
	Processed int `json:"processed"`
	Skipped   int `json:"skipped"`
}

func New(dataPath string) *Processor {
	return &Processor{
		DataPath: dataPath,
		Now:      func() time.Time { return time.Now().UTC() },
	}
}

func (p *Processor) ProcessOnce() (Result, error) {
	if err := p.ensureDirectories(); err != nil {
		return Result{}, err
	}

	events, err := filepath.Glob(filepath.Join(p.DataPath, "outbox", "*.json"))
	if err != nil {
		return Result{}, err
	}

	result := Result{}
	for _, eventPath := range events {
		processed, err := p.processEvent(eventPath)
		if err != nil {
			return result, err
		}
		if processed {
			result.Processed++
		} else {
			result.Skipped++
		}
	}

	return result, nil
}

func (p *Processor) processEvent(eventPath string) (bool, error) {
	var event contracts.OutboxEvent
	if err := readJSON(eventPath, &event); err != nil {
		return false, err
	}

	processedPath := filepath.Join(p.DataPath, "processed", filepath.Base(eventPath))
	if _, err := os.Stat(processedPath); err == nil {
		_ = os.Remove(eventPath)
		return false, nil
	}

	if event.Type != "OrderCreated" {
		return false, p.moveProcessed(eventPath)
	}

	orderPath := filepath.Join(p.DataPath, "orders", strings.ToLower(strings.ReplaceAll(event.OrderID, "-", ""))+".json")
	var order contracts.Order
	if err := readJSON(orderPath, &order); err != nil {
		if errors.Is(err, os.ErrNotExist) {
			return false, fmt.Errorf("order %s not found for event %s", event.OrderID, event.ID)
		}
		return false, err
	}

	if order.Status == "Completed" {
		return false, p.moveProcessed(eventPath)
	}

	now := p.Now()
	order.Status = "Completed"
	order.UpdatedAtUTC = now

	entries := []contracts.LedgerEntry{
		{
			ID:           newID(),
			OrderID:      order.ID,
			Type:         "PaymentCaptured",
			Amount:       order.TotalAmount,
			Currency:     order.Currency,
			Description:  "Simulated payment captured by Go worker.",
			CreatedAtUTC: now,
		},
		{
			ID:           newID(),
			OrderID:      order.ID,
			Type:         "RevenueRecognized",
			Amount:       order.TotalAmount,
			Currency:     order.Currency,
			Description:  "Revenue recognized after order completion.",
			CreatedAtUTC: now,
		},
	}

	for _, entry := range entries {
		path := filepath.Join(p.DataPath, "ledger", fmt.Sprintf("%s-%s.json", normalizeID(order.ID), normalizeID(entry.ID)))
		if err := writeJSON(path, entry); err != nil {
			return false, err
		}
	}

	if err := writeJSON(orderPath, order); err != nil {
		return false, err
	}

	return true, p.moveProcessed(eventPath)
}

func (p *Processor) moveProcessed(eventPath string) error {
	processedPath := filepath.Join(p.DataPath, "processed", filepath.Base(eventPath))
	if err := os.Rename(eventPath, processedPath); err != nil {
		if errors.Is(err, os.ErrNotExist) {
			return nil
		}
		return err
	}
	return nil
}

func (p *Processor) ensureDirectories() error {
	for _, dir := range []string{"orders", "outbox", "ledger", "processed"} {
		if err := os.MkdirAll(filepath.Join(p.DataPath, dir), 0o755); err != nil {
			return err
		}
	}
	return nil
}

func readJSON[T any](path string, value *T) error {
	file, err := os.Open(path)
	if err != nil {
		return err
	}
	defer file.Close()

	return json.NewDecoder(file).Decode(value)
}

func writeJSON[T any](path string, value T) error {
	tempPath := path + ".tmp"
	file, err := os.Create(tempPath)
	if err != nil {
		return err
	}
	encoder := json.NewEncoder(file)
	encoder.SetIndent("", "  ")
	if err := encoder.Encode(value); err != nil {
		_ = file.Close()
		return err
	}
	if err := file.Close(); err != nil {
		return err
	}
	return os.Rename(tempPath, path)
}

func newID() string {
	var bytes [16]byte
	if _, err := rand.Read(bytes[:]); err != nil {
		return normalizeID(fmt.Sprintf("%d", time.Now().UnixNano()))
	}
	return hex.EncodeToString(bytes[:])
}

func normalizeID(id string) string {
	return strings.ToLower(strings.ReplaceAll(id, "-", ""))
}
