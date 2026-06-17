package contracts

import "time"

type Order struct {
	ID             string      `json:"id"`
	CustomerID     string      `json:"customerId"`
	Status         string      `json:"status"`
	Currency       string      `json:"currency"`
	TotalAmount    float64     `json:"totalAmount"`
	Items          []OrderItem `json:"items"`
	IdempotencyKey *string     `json:"idempotencyKey"`
	CreatedAtUTC   time.Time   `json:"createdAtUtc"`
	UpdatedAtUTC   time.Time   `json:"updatedAtUtc"`
}

type OrderItem struct {
	SKU       string  `json:"sku"`
	Quantity  int     `json:"quantity"`
	UnitPrice float64 `json:"unitPrice"`
	LineTotal float64 `json:"lineTotal"`
}

type OutboxEvent struct {
	ID            string    `json:"id"`
	Type          string    `json:"type"`
	OrderID       string    `json:"orderId"`
	OccurredAtUTC time.Time `json:"occurredAtUtc"`
	SchemaVersion string    `json:"schemaVersion"`
}

type LedgerEntry struct {
	ID           string    `json:"id"`
	OrderID      string    `json:"orderId"`
	Type         string    `json:"type"`
	Amount       float64   `json:"amount"`
	Currency     string    `json:"currency"`
	Description  string    `json:"description"`
	CreatedAtUTC time.Time `json:"createdAtUtc"`
}
