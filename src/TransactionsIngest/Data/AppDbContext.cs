using Microsoft.EntityFrameworkCore;
using TransactionsIngest.Models;

namespace TransactionsIngest.Data;

public class AppDbContext : DbContext
{
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<TransactionAudit> TransactionAudits { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Transaction entity configuration
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.TransactionId)
                  .IsUnique();

            entity.Property(e => e.TransactionId)
                  .IsRequired()
                  .HasMaxLength(50);

            entity.Property(e => e.CardLast4)
                  .IsRequired()
                  .HasMaxLength(4);

            entity.Property(e => e.LocationCode)
                  .IsRequired()
                  .HasMaxLength(20);

            entity.Property(e => e.ProductName)
                  .IsRequired()
                  .HasMaxLength(20);

            entity.Property(e => e.Amount)
                  .HasColumnType("decimal(18,2)");

            entity.Property(e => e.Status)
                  .HasConversion<string>();  // Store enum as string in DB
        });

        // TransactionAudit entity configuration
        modelBuilder.Entity<TransactionAudit>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TransactionId)
                  .IsRequired()
                  .HasMaxLength(50);

            entity.Property(e => e.ChangeType)
                  .IsRequired()
                  .HasMaxLength(20);

            entity.Property(e => e.FieldName)
                  .HasMaxLength(50);

            entity.Property(e => e.OldValue)
                  .HasMaxLength(200);

            entity.Property(e => e.NewValue)
                  .HasMaxLength(200);

            // Relationship: one Transaction → many Audits
            entity.HasOne(e => e.Transaction)
                  .WithMany(t => t.Audits)
                  .HasForeignKey(e => e.TransactionFk)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}