using Microsoft.EntityFrameworkCore;
using TransactionsIngest.Data;
using TransactionsIngest.Models;

namespace TransactionsIngest.Services;

public class IngestService
{
    private readonly AppDbContext _db;
    private readonly int _lookbackHours;

    public IngestService(AppDbContext db, int lookbackHours)
    {
        _db = db;
        _lookbackHours = lookbackHours;
    }

    public async Task RunAsync(List<ApiTransaction> apiTransactions)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddHours(-_lookbackHours);

        // Wrap entire run in a single DB transaction for idempotency
        await using var dbTransaction = await _db.Database.BeginTransactionAsync();

        try
        {
            // Track which TransactionIds came in from the API
            var incomingIds = apiTransactions.Select(t => t.TransactionId).ToHashSet();

            // Process each incoming transaction (insert or update)
            foreach (var apiTx in apiTransactions)
            {
                await UpsertTransactionAsync(apiTx, now);
            }

            // Revoke any active transactions within lookback window missing from snapshot
            await RevokeAbsentTransactionsAsync(incomingIds, cutoff, now);

            // Finalize any active transactions older than lookback window
            await FinalizeOldTransactionsAsync(cutoff, now);

            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            Console.WriteLine("Ingest run completed successfully.");
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            Console.WriteLine($"Ingest run failed, transaction rolled back: {ex.Message}");
            throw;
        }
    }

    private async Task UpsertTransactionAsync(ApiTransaction apiTx, DateTime now)
    {
        var existing = await _db.Transactions
            .Include(t => t.Audits)
            .FirstOrDefaultAsync(t => t.TransactionId == apiTx.TransactionId);

        var cardLast4 = apiTx.CardNumber.Length >= 4
            ? apiTx.CardNumber[^4..]
            : apiTx.CardNumber;

        if (existing == null)
        {
            // INSERT: new transaction
            var newTx = new Transaction
            {
                TransactionId = apiTx.TransactionId,
                CardLast4 = cardLast4,
                LocationCode = apiTx.LocationCode,
                ProductName = apiTx.ProductName,
                Amount = apiTx.Amount,
                TransactionTime = apiTx.Timestamp,
                Status = TransactionStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.Transactions.Add(newTx);

            _db.TransactionAudits.Add(new TransactionAudit
            {
                TransactionId = apiTx.TransactionId,
                ChangeType = "Created",
                ChangedAt = now,
                Transaction = newTx
            });

            Console.WriteLine($"  [INSERT] {apiTx.TransactionId}");
        }
        else
        {
            // Skip finalized transactions — they must not change
            if (existing.Status == TransactionStatus.Finalized)
            {
                Console.WriteLine($"  [SKIP] {apiTx.TransactionId} is finalized, no changes allowed.");
                return;
            }

            // UPDATE: detect field-level changes
            var changes = DetectChanges(existing, apiTx, cardLast4);

            if (changes.Count > 0)
            {
                // Apply changes
                existing.CardLast4 = cardLast4;
                existing.LocationCode = apiTx.LocationCode;
                existing.ProductName = apiTx.ProductName;
                existing.Amount = apiTx.Amount;
                existing.TransactionTime = apiTx.Timestamp;
                existing.Status = TransactionStatus.Active;
                existing.UpdatedAt = now;

                // Record each changed field in audit
                foreach (var (field, oldVal, newVal) in changes)
                {
                    _db.TransactionAudits.Add(new TransactionAudit
                    {
                        TransactionId = apiTx.TransactionId,
                        ChangeType = "Updated",
                        FieldName = field,
                        OldValue = oldVal,
                        NewValue = newVal,
                        ChangedAt = now,
                        TransactionFk = existing.Id
                    });
                }

                Console.WriteLine($"  [UPDATE] {apiTx.TransactionId} — {changes.Count} field(s) changed");
            }
            else
            {
                Console.WriteLine($"  [NO CHANGE] {apiTx.TransactionId}");
            }
        }
    }

    private List<(string Field, string OldValue, string NewValue)> DetectChanges(
        Transaction existing, ApiTransaction incoming, string cardLast4)
    {
        var changes = new List<(string, string, string)>();

        if (existing.CardLast4 != cardLast4)
            changes.Add(("CardLast4", existing.CardLast4, cardLast4));

        if (existing.LocationCode != incoming.LocationCode)
            changes.Add(("LocationCode", existing.LocationCode, incoming.LocationCode));

        if (existing.ProductName != incoming.ProductName)
            changes.Add(("ProductName", existing.ProductName, incoming.ProductName));

        if (existing.Amount != incoming.Amount)
            changes.Add(("Amount", existing.Amount.ToString(), incoming.Amount.ToString()));

        if (existing.TransactionTime != incoming.Timestamp)
            changes.Add(("TransactionTime", existing.TransactionTime.ToString("o"), incoming.Timestamp.ToString("o")));

        return changes;
    }

    private async Task RevokeAbsentTransactionsAsync(
        HashSet<string> incomingIds, DateTime cutoff, DateTime now)
    {
        // Find active transactions within the lookback window NOT in the current snapshot
        var toRevoke = await _db.Transactions
            .Where(t => t.Status == TransactionStatus.Active
                     && t.TransactionTime >= cutoff
                     && !incomingIds.Contains(t.TransactionId))
            .ToListAsync();

        foreach (var tx in toRevoke)
        {
            tx.Status = TransactionStatus.Revoked;
            tx.UpdatedAt = now;

            _db.TransactionAudits.Add(new TransactionAudit
            {
                TransactionId = tx.TransactionId,
                ChangeType = "Revoked",
                ChangedAt = now,
                TransactionFk = tx.Id
            });

            Console.WriteLine($"  [REVOKE] {tx.TransactionId}");
        }
    }

    private async Task FinalizeOldTransactionsAsync(DateTime cutoff, DateTime now)
    {
        // Finalize active transactions older than the lookback window
        var toFinalize = await _db.Transactions
            .Where(t => t.Status == TransactionStatus.Active
                     && t.TransactionTime < cutoff)
            .ToListAsync();

        foreach (var tx in toFinalize)
        {
            tx.Status = TransactionStatus.Finalized;
            tx.UpdatedAt = now;

            _db.TransactionAudits.Add(new TransactionAudit
            {
                TransactionId = tx.TransactionId,
                ChangeType = "Finalized",
                ChangedAt = now,
                TransactionFk = tx.Id
            });

            Console.WriteLine($"  [FINALIZE] {tx.TransactionId}");
        }
    }
}