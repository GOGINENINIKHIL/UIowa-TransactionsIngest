using Microsoft.EntityFrameworkCore;
using TransactionsIngest.Data;
using TransactionsIngest.Models;
using TransactionsIngest.Services;
using Microsoft.Data.Sqlite;

namespace TransactionsIngest.Tests;

public class IngestServiceTests
{
    // Creates a fresh in-memory SQLite database for each test
    private static AppDbContext CreateDb()
    {
        // Keep a persistent connection open so in-memory DB tables survive the test
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static List<ApiTransaction> GetBaseTransactions() => new()
    {
        new ApiTransaction
        {
            TransactionId = "T-1001",
            CardNumber = "4111111111111111",
            LocationCode = "STO-01",
            ProductName = "Wireless Mouse",
            Amount = 19.99m,
            Timestamp = new DateTime(2026, 3, 11, 0, 0, 0, DateTimeKind.Utc)
        },
        new ApiTransaction
        {
            TransactionId = "T-1002",
            CardNumber = "4000000000000002",
            LocationCode = "STO-02",
            ProductName = "USB-C Cable",
            Amount = 25.00m,
            Timestamp = new DateTime(2026, 3, 10, 22, 0, 0, DateTimeKind.Utc)
        }
    };

    // ---------------------------------------------------------------
    // Test 1: New transactions are inserted correctly
    // ---------------------------------------------------------------
    [Fact]
    public async Task NewTransactions_AreInserted()
    {
        using var db = CreateDb();
        var service = new IngestService(db, 24);

        await service.RunAsync(GetBaseTransactions());

        var transactions = await db.Transactions.ToListAsync();
        Assert.Equal(2, transactions.Count);
        Assert.All(transactions, t => Assert.Equal(TransactionStatus.Active, t.Status));
    }

    // ---------------------------------------------------------------
    // Test 2: Audit records are created for new inserts
    // ---------------------------------------------------------------
    [Fact]
    public async Task NewTransactions_CreateAuditRecords()
    {
        using var db = CreateDb();
        var service = new IngestService(db, 24);

        await service.RunAsync(GetBaseTransactions());

        var audits = await db.TransactionAudits.ToListAsync();
        Assert.Equal(2, audits.Count);
        Assert.All(audits, a => Assert.Equal("Created", a.ChangeType));
    }

    // ---------------------------------------------------------------
    // Test 3: Changed fields are detected and recorded
    // ---------------------------------------------------------------
    [Fact]
    public async Task UpdatedTransaction_DetectsFieldChanges()
    {
        using var db = CreateDb();
        var service = new IngestService(db, 24);

        // First run — insert
        await service.RunAsync(GetBaseTransactions());

        // Second run — T-1001 has a different amount
        var updated = GetBaseTransactions();
        updated[0].Amount = 99.99m;

        await service.RunAsync(updated);

        var auditRecords = await db.TransactionAudits
            .Where(a => a.TransactionId == "T-1001" && a.ChangeType == "Updated")
            .ToListAsync();

        Assert.Single(auditRecords);
        Assert.Equal("Amount", auditRecords[0].FieldName);
        Assert.Equal("19.99", auditRecords[0].OldValue);
        Assert.Equal("99.99", auditRecords[0].NewValue);
    }

    // ---------------------------------------------------------------
    // Test 4: Idempotency — repeated runs produce no duplicates
    // ---------------------------------------------------------------
    [Fact]
    public async Task RepeatedRuns_AreIdempotent()
    {
        using var db = CreateDb();
        var service = new IngestService(db, 24);

        await service.RunAsync(GetBaseTransactions());
        await service.RunAsync(GetBaseTransactions());
        await service.RunAsync(GetBaseTransactions());

        // Still only 2 transactions
        var transactions = await db.Transactions.ToListAsync();
        Assert.Equal(2, transactions.Count);

        // Only 2 audit records (one Created per transaction, no spurious updates)
        var audits = await db.TransactionAudits.ToListAsync();
        Assert.Equal(2, audits.Count);
    }

    // ---------------------------------------------------------------
    // Test 5: Absent transactions within 24hrs are revoked
    // ---------------------------------------------------------------
    [Fact]
    public async Task AbsentTransactions_AreRevoked()
    {
        using var db = CreateDb();
        var service = new IngestService(db, 24);

        // First run — insert both
        await service.RunAsync(GetBaseTransactions());

        // Second run — T-1002 is missing from snapshot
        var reduced = GetBaseTransactions().Take(1).ToList();
        await service.RunAsync(reduced);

        var t1002 = await db.Transactions
            .FirstAsync(t => t.TransactionId == "T-1002");

        Assert.Equal(TransactionStatus.Revoked, t1002.Status);

        var revokeAudit = await db.TransactionAudits
            .FirstOrDefaultAsync(a => a.TransactionId == "T-1002"
                                   && a.ChangeType == "Revoked");
        Assert.NotNull(revokeAudit);
    }

    // ---------------------------------------------------------------
    // Test 6: Finalization — old transactions are finalized
    // ---------------------------------------------------------------
    [Fact]
    public async Task OldTransactions_AreFinalized()
    {
        using var db = CreateDb();
        var service = new IngestService(db, 24);

        // Insert a transaction with a timestamp older than 24 hours
        var oldTx = new Transaction
        {
            TransactionId = "T-OLD",
            CardLast4 = "1111",
            LocationCode = "STO-01",
            ProductName = "Old Item",
            Amount = 10.00m,
            TransactionTime = DateTime.UtcNow.AddHours(-25),
            Status = TransactionStatus.Active,
            CreatedAt = DateTime.UtcNow.AddHours(-25),
            UpdatedAt = DateTime.UtcNow.AddHours(-25)
        };
        db.Transactions.Add(oldTx);
        await db.SaveChangesAsync();

        // Run ingest — T-OLD should get finalized
        await service.RunAsync(GetBaseTransactions());

        var finalized = await db.Transactions
            .FirstAsync(t => t.TransactionId == "T-OLD");

        Assert.Equal(TransactionStatus.Finalized, finalized.Status);

        var finalizeAudit = await db.TransactionAudits
            .FirstOrDefaultAsync(a => a.TransactionId == "T-OLD"
                                   && a.ChangeType == "Finalized");
        Assert.NotNull(finalizeAudit);
    }

    // ---------------------------------------------------------------
    // Test 7: Finalized transactions cannot be changed
    // ---------------------------------------------------------------
    [Fact]
    public async Task FinalizedTransactions_AreNotModified()
    {
        using var db = CreateDb();
        var service = new IngestService(db, 24);

        // Insert a finalized transaction directly
        var finalizedTx = new Transaction
        {
            TransactionId = "T-1001",
            CardLast4 = "1111",
            LocationCode = "STO-01",
            ProductName = "Wireless Mouse",
            Amount = 19.99m,
            TransactionTime = new DateTime(2026, 3, 11, 0, 0, 0, DateTimeKind.Utc),
            Status = TransactionStatus.Finalized,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Transactions.Add(finalizedTx);
        await db.SaveChangesAsync();

        // Run ingest with a different amount for T-1001
        var modified = GetBaseTransactions();
        modified[0].Amount = 999.99m;

        await service.RunAsync(modified);

        var tx = await db.Transactions.FirstAsync(t => t.TransactionId == "T-1001");

        // Amount must not have changed
        Assert.Equal(19.99m, tx.Amount);
        Assert.Equal(TransactionStatus.Finalized, tx.Status);
    }
}