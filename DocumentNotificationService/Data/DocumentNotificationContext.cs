using Microsoft.EntityFrameworkCore;
using DocumentNotificationService.Models;
using System.Text.RegularExpressions;

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

        // Configure snake_case naming convention
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            // Configure table names to snake_case
            entity.SetTableName(ToSnakeCase(entity.GetTableName()));

            // Configure column names to snake_case
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }

            // Configure keys and indexes
            foreach (var key in entity.GetKeys())
            {
                key.SetName(ToSnakeCase(key.GetName()));
            }

            foreach (var index in entity.GetIndexes())
            {
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName()));
            }
        }

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

    private static string ToSnakeCase(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return input ?? string.Empty;

        return Regex.Replace(input, "([a-z0-9])([A-Z])", "$1_$2").ToLower();
    }
}