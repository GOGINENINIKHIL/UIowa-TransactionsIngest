namespace TransactionsIngest.Models;

public class TransactionAudit
{
    public int Id { get; set; }
    public string TransactionId { get; set; } = string.Empty;  // Links to Transaction.TransactionId
    public string ChangeType { get; set; } = string.Empty;     // "Created", "Updated", "Revoked", "Finalized"
    public string? FieldName { get; set; }                      // Which field changed (null for Created/Revoked)
    public string? OldValue { get; set; }                       // Previous value
    public string? NewValue { get; set; }                       // New value
    public DateTime ChangedAt { get; set; }                     // UTC

    // Foreign key
    public int TransactionFk { get; set; }
    public Transaction Transaction { get; set; } = null!;
}