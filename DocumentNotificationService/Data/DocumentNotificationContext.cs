using Microsoft.EntityFrameworkCore;
using DocumentNotificationService.Models;

namespace DocumentNotificationService.Data;

public class DocumentNotificationContext : DbContext
{
    public DocumentNotificationContext(DbContextOptions<DocumentNotificationContext> options)
        : base(options)
    {
    }

    public DbSet<ProcessedDocument> ProcessedDocuments { get; set; }
    public DbSet<LastQueryTimestamp> LastQueryTimestamps { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure indexes for better performance
        modelBuilder.Entity<ProcessedDocument>()
            .HasIndex(p => p.DocumentId)
            .IsUnique();

        modelBuilder.Entity<ProcessedDocument>()
            .HasIndex(p => p.PortfolioId);

        modelBuilder.Entity<ProcessedDocument>()
            .HasIndex(p => p.DocumentDate);

        // Seed initial data for LastQueryTimestamp
        modelBuilder.Entity<LastQueryTimestamp>()
            .HasData(new LastQueryTimestamp 
            { 
                Id = 1, 
                LastSuccessfulQuery = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow
            });
    }
}