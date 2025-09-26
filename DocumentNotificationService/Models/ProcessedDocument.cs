using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentNotificationService.Models;

[Table("processed_documents")]
public class ProcessedDocument
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string DocumentId { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public DateTime DocumentDate { get; set; }

    [Required]
    [MaxLength(100)]
    public string PortfolioId { get; set; } = string.Empty;

    [Required]
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    public bool MessageSent { get; set; } = false;

    public string? ErrorMessage { get; set; }
}

[Table("last_query_timestamps")]
public class LastQueryTimestamp
{
    [Key]
    public int Id { get; set; }

    [Required]
    public DateTime LastSuccessfulQuery { get; set; } = DateTime.UtcNow.AddDays(-1);

    [Required]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}