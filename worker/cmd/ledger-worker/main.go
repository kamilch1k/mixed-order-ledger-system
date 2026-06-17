package main

import (
	"encoding/json"
	"fmt"
	"os"
	"time"

	"github.com/kamilch1k/mixed-order-ledger-system/worker/internal/processor"
)

func main() {
	dataPath := getenv("DATA_PATH", "data")
	interval := getenv("POLL_INTERVAL", "")
	worker := processor.New(dataPath)

	if interval == "" {
		result, err := worker.ProcessOnce()
		exitOnError(err)
		_ = json.NewEncoder(os.Stdout).Encode(result)
		return
	}

	duration, err := time.ParseDuration(interval)
	exitOnError(err)

	for {
		result, err := worker.ProcessOnce()
		exitOnError(err)
		fmt.Printf("processed=%d skipped=%d\n", result.Processed, result.Skipped)
		time.Sleep(duration)
	}
}

func getenv(key, fallback string) string {
	value := os.Getenv(key)
	if value == "" {
		return fallback
	}
	return value
}

func exitOnError(err error) {
	if err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}
}
