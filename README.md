# UIowa-TransactionsIngest

# TransactionsIngest

A .NET 10 Console application that performs reliable hourly ingestion of retail payment transactions. It fetches a 24-hour snapshot from a gateway API, upserts records by Transaction ID, detects and records field-level changes, marks revoked transactions, and finalizes old records — all within a single database transaction per run for idempotency.

---

## Technology Stack

- .NET 10 Console Application
- Entity Framework Core (code-first, SQLite)
- xUnit for automated testing
- Configuration via `appsettings.json`

---

## Project Structure
```
TransactionsIngest/
├── src/
│   └── TransactionsIngest/
│       ├── Data/
│       │   └── AppDbContext.cs         # EF Core DbContext
│       ├── Models/
│       │   ├── Transaction.cs          # Main transaction entity
│       │   ├── TransactionAudit.cs     # Audit trail entity
│       │   └── AppSettings.cs          # Typed configuration
│       ├── Services/
│       │   ├── MockApiService.cs       # Simulates API fetch
│       │   └── IngestService.cs        # Core ingest logic
│       ├── Program.cs                  # Entry point
│       ├── appsettings.json            # Configuration
│       └── mock-feed.json              # Sample JSON feed for local testing
└── tests/
    └── TransactionsIngest.Tests/
        └── UnitTest1.cs                # Automated tests (7 tests)
```

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- Git

---

## Build & Run

### 1. Clone the repository
```bash
git clone https://github.com/GOGINENINIKHIL/UIowa-TransactionsIngest.git
cd UIowa-TransactionsIngest
```

### 2. Build the solution
```bash
dotnet build
```

### 3. Run the console app
```bash
dotnet run --project src/TransactionsIngest
```

The app will create a `transactions.db` SQLite database on first run and print a summary of all inserts, updates, revocations, and finalizations to the console.

### 4. Run again to verify idempotency
```bash
dotnet run --project src/TransactionsIngest
```
All transactions should show `[NO CHANGE]` on repeated runs with unchanged input.

### 5. Run automated tests
```bash
dotnet test
```
Expected: **7 tests passing**.

---

## Configuration

All settings are in `src/TransactionsIngest/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=transactions.db"
  },
  "ApiSettings": {
    "BaseUrl": "https://api.example.com",
    "TransactionsEndpoint": "/api/transactions",
    "UseMockFeed": true,
    "MockFeedPath": "src/TransactionsIngest/mock-feed.json"
  },
  "IngestSettings": {
    "LookbackHours": 24
  }
}
```

Set `UseMockFeed: true` to use the built-in mock data. Set to `false` to wire in a real API (requires implementing the HTTP call in `MockApiService.cs`).

---

## Mock JSON Feed

A sample `mock-feed.json` file is provided at `src/TransactionsIngest/mock-feed.json` for local testing. This file contains 5 sample transactions that match the API's expected JSON format.

To use it, ensure `appsettings.json` is configured as follows:
```json
"ApiSettings": {
  "UseMockFeed": true,
  "MockFeedPath": "src/TransactionsIngest/mock-feed.json"
}
```

You can edit `mock-feed.json` to test different scenarios such as:
- Adding new transactions to test inserts
- Removing transactions to test revocation
- Changing field values to test update detection

To use hardcoded mock data instead, set `MockFeedPath` to an empty string:
```json
"MockFeedPath": ""
## Mock JSON Feed

A sample `mock-feed.json` file is provided at `src/TransactionsIngest/mock-feed.json` for local testing. This file contains 5 sample transactions that match the API's expected JSON format.

To use it, ensure `appsettings.json` is configured as follows:
```json
"ApiSettings": {
  "UseMockFeed": true,
  "MockFeedPath": "src/TransactionsIngest/mock-feed.json"
}
```

You can edit `mock-feed.json` to test different scenarios such as:
- Adding new transactions to test inserts
- Removing transactions to test revocation
- Changing field values to test update detection

To use hardcoded mock data instead, set `MockFeedPath` to an empty string:
```json
"MockFeedPath": ""
```
---

## Approach & Design Decisions

### Upsert by TransactionId
Each incoming transaction is looked up by `TransactionId`. If it does not exist, it is inserted with a `Created` audit record. If it exists, each tracked field is compared individually. Any changed field produces an `Updated` audit record capturing the field name, old value, and new value.

### Revocation
After processing all incoming transactions, the service queries for any `Active` records within the 24-hour lookback window that were absent from the current snapshot. These are marked `Revoked` with a corresponding audit record.

### Finalization
Any `Active` transaction older than the lookback window is marked `Finalized`. Finalized records are immutable — subsequent runs skip them entirely.

### Idempotency
The entire ingest run is wrapped in a single database transaction. If no fields change, no audit records are written. Repeated runs with identical input produce zero side effects.

### Privacy
The full card number received from the API is never persisted. Only the last 4 digits are stored in the database.

### Audit Trail
Every state change (insert, update, revoke, finalize) is recorded in the `TransactionAudits` table with a timestamp, change type, and for updates, the specific field and before/after values.

---

## Assumptions

- The app is triggered by an external scheduler (e.g. Windows Task Scheduler, cron); no internal scheduling is implemented.
- `TransactionId` is a stable, unique string identifier across all upstream systems.
- All timestamps are UTC.
- The mock feed uses fixed timestamps to ensure deterministic, idempotent behavior across test runs.
- `ProductName` and `LocationCode` are truncated to 20 characters per the data model specification.