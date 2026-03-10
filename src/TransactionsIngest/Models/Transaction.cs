namespace TransactionsIngest.Models;

public enum TransactionStatus
{
    Active,
    Revoked,
    Finalized
}

public class Transaction
{
    public int Id { get; set; }                          // Primary key (DB)
    public string TransactionId { get; set; } = string.Empty; // Stable unique ID from API
    public string CardLast4 { get; set; } = string.Empty;     // Only last 4 digits stored (privacy)
    public string LocationCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime TransactionTime { get; set; }        // UTC
    public TransactionStatus Status { get; set; } = TransactionStatus.Active;
    public DateTime CreatedAt { get; set; }              // UTC - when first inserted
    public DateTime UpdatedAt { get; set; }              // UTC - when last modified

    // Navigation property
    public ICollection<TransactionAudit> Audits { get; set; } = new List<TransactionAudit>();
}