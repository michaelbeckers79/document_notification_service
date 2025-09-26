using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DocumentNotificationService.Configuration;
using DocumentNotificationService.Data;
using DocumentNotificationService.Models;

namespace DocumentNotificationService.Services;

public class DocumentProcessingService
{
    private readonly ILogger<DocumentProcessingService> _logger;
    private readonly DocumentNotificationContext _context;
    private readonly DSXDocumentService _dsxService;
    private readonly RabbitMQService _rabbitMqService;
    private readonly EmailNotificationService _emailService;
    private readonly AppSettings _settings;

    public DocumentProcessingService(
        ILogger<DocumentProcessingService> logger,
        DocumentNotificationContext context,
        DSXDocumentService dsxService,
        RabbitMQService rabbitMqService,
        EmailNotificationService emailService,
        IOptions<AppSettings> options)
    {
        _logger = logger;
        _context = context;
        _dsxService = dsxService;
        _rabbitMqService = rabbitMqService;
        _emailService = emailService;
        _settings = options.Value;
    }

    public async Task<ProcessingResult> ProcessDocumentsAsync(DateTime? since = null, bool dryRun = false)
    {
        var result = new ProcessingResult();
        var errors = new List<string>();

        try
        {
            _logger.LogInformation("Starting document processing. DryRun: {DryRun}", dryRun);

            // Get last query timestamp if not provided
            var queryTimestamp = since ?? await GetLastQueryTimestampAsync();
            _logger.LogInformation("Processing documents since: {Timestamp}", queryTimestamp);

            // Search for new documents
            var documents = await _dsxService.SearchDocumentsAsync(queryTimestamp, _settings.DSXService.DocumentTypes);
            _logger.LogInformation("Found {Count} documents to process", documents.Count);

            if (!documents.Any())
            {
                _logger.LogInformation("No new documents found");
                return result;
            }

            // Filter out already processed documents
            var existingDocumentIds = await _context.ProcessedDocuments
                .Where(pd => documents.Select(d => d.DocumentId).Contains(pd.DocumentId))
                .Select(pd => pd.DocumentId)
                .ToListAsync();

            var newDocuments = documents.Where(d => !existingDocumentIds.Contains(d.DocumentId)).ToList();
            _logger.LogInformation("Found {Count} new documents (filtered out {Existing} already processed)", 
                newDocuments.Count, existingDocumentIds.Count);

            if (!newDocuments.Any())
            {
                await UpdateLastQueryTimestampAsync();
                _logger.LogInformation("All documents have already been processed");
                return result;
            }

            // Process each document
            foreach (var document in newDocuments)
            {
                try
                {
                    await ProcessSingleDocumentAsync(document, dryRun);
                    result.ProcessedCount++;
                }
                catch (Exception ex)
                {
                    result.ErrorCount++;
                    var errorMessage = $"Failed to process document {document.DocumentId}: {ex.Message}";
                    errors.Add(errorMessage);
                    _logger.LogError(ex, "Error processing document {DocumentId}", document.DocumentId);

                    // Still save the document record with error information
                    if (!dryRun)
                    {
                        await SaveDocumentWithErrorAsync(document, ex.Message);
                    }
                }
            }

            // Update last query timestamp if not dry run
            if (!dryRun)
            {
                await UpdateLastQueryTimestampAsync();
            }

            _logger.LogInformation("Document processing completed. Processed: {Processed}, Errors: {Errors}", 
                result.ProcessedCount, result.ErrorCount);

            // Send summary email
            if (result.ProcessedCount > 0 || result.ErrorCount > 0)
            {
                await _emailService.SendProcessingSummaryAsync(result.ProcessedCount, result.ErrorCount, errors);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during document processing");
            await _emailService.SendErrorNotificationAsync("Document Processing Failed", 
                "A critical error occurred during document processing", ex);
            throw;
        }

        return result;
    }

    private async Task ProcessSingleDocumentAsync(DocumentInfo document, bool dryRun)
    {
        _logger.LogDebug("Processing document {DocumentId} for Portfolio {PortfolioId}", 
            document.DocumentId, document.PortfolioId);

        // Send RabbitMQ notification
        if (!dryRun)
        {
            await _rabbitMqService.PublishDocumentNotificationAsync(document.PortfolioId);
        }
        else
        {
            _logger.LogInformation("[DRY RUN] Would publish notification for Portfolio ID: {PortfolioId}", 
                document.PortfolioId);
        }

        // Save document to database
        if (!dryRun)
        {
            await SaveProcessedDocumentAsync(document);
        }
        else
        {
            _logger.LogInformation("[DRY RUN] Would save document: {DocumentId}", document.DocumentId);
        }
    }

    private async Task SaveProcessedDocumentAsync(DocumentInfo document)
    {
        var processedDocument = new ProcessedDocument
        {
            DocumentId = document.DocumentId,
            Name = document.Name,
            DocumentDate = document.DocumentDate,
            PortfolioId = document.PortfolioId,
            ProcessedAt = DateTime.UtcNow,
            MessageSent = true
        };

        _context.ProcessedDocuments.Add(processedDocument);
        await _context.SaveChangesAsync();

        _logger.LogDebug("Saved processed document {DocumentId} to database", document.DocumentId);
    }

    private async Task SaveDocumentWithErrorAsync(DocumentInfo document, string errorMessage)
    {
        try
        {
            var processedDocument = new ProcessedDocument
            {
                DocumentId = document.DocumentId,
                Name = document.Name,
                DocumentDate = document.DocumentDate,
                PortfolioId = document.PortfolioId,
                ProcessedAt = DateTime.UtcNow,
                MessageSent = false,
                ErrorMessage = errorMessage
            };

            _context.ProcessedDocuments.Add(processedDocument);
            await _context.SaveChangesAsync();

            _logger.LogDebug("Saved failed document {DocumentId} to database with error", document.DocumentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save error document {DocumentId} to database", document.DocumentId);
        }
    }

    private async Task<DateTime> GetLastQueryTimestampAsync()
    {
        var lastTimestamp = await _context.LastQueryTimestamps
            .OrderByDescending(lt => lt.UpdatedAt)
            .FirstOrDefaultAsync();

        return lastTimestamp?.LastSuccessfulQuery ?? DateTime.UtcNow.AddDays(-1);
    }

    private async Task UpdateLastQueryTimestampAsync()
    {
        var timestamp = await _context.LastQueryTimestamps
            .OrderByDescending(lt => lt.UpdatedAt)
            .FirstOrDefaultAsync();

        if (timestamp == null)
        {
            timestamp = new LastQueryTimestamp
            {
                LastSuccessfulQuery = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.LastQueryTimestamps.Add(timestamp);
        }
        else
        {
            timestamp.LastSuccessfulQuery = DateTime.UtcNow;
            timestamp.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        _logger.LogDebug("Updated last query timestamp to {Timestamp}", timestamp.LastSuccessfulQuery);
    }

    public async Task<List<ProcessedDocument>> GetRecentDocumentsAsync(int limit = 10)
    {
        return await _context.ProcessedDocuments
            .OrderByDescending(pd => pd.ProcessedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<ProcessedDocument>> GetFailedDocumentsAsync()
    {
        return await _context.ProcessedDocuments
            .Where(pd => !pd.MessageSent || !string.IsNullOrEmpty(pd.ErrorMessage))
            .OrderByDescending(pd => pd.ProcessedAt)
            .ToListAsync();
    }

    public async Task<ProcessingResult> RetryFailedDocumentsAsync(string? specificDocumentId = null)
    {
        var result = new ProcessingResult();
        var errors = new List<string>();

        try
        {
            var failedDocuments = specificDocumentId != null
                ? await _context.ProcessedDocuments
                    .Where(pd => pd.DocumentId == specificDocumentId && (!pd.MessageSent || !string.IsNullOrEmpty(pd.ErrorMessage)))
                    .ToListAsync()
                : await GetFailedDocumentsAsync();

            _logger.LogInformation("Retrying {Count} failed documents", failedDocuments.Count);

            foreach (var document in failedDocuments)
            {
                try
                {
                    // Retry sending notification
                    await _rabbitMqService.PublishDocumentNotificationAsync(document.PortfolioId);

                    // Update document status
                    document.MessageSent = true;
                    document.ErrorMessage = null;
                    document.ProcessedAt = DateTime.UtcNow;

                    result.ProcessedCount++;
                    _logger.LogInformation("Successfully retried document {DocumentId}", document.DocumentId);
                }
                catch (Exception ex)
                {
                    result.ErrorCount++;
                    var errorMessage = $"Failed to retry document {document.DocumentId}: {ex.Message}";
                    errors.Add(errorMessage);
                    
                    document.ErrorMessage = ex.Message;
                    _logger.LogError(ex, "Failed retry for document {DocumentId}", document.DocumentId);
                }
            }

            await _context.SaveChangesAsync();

            // Send summary email
            if (result.ProcessedCount > 0 || result.ErrorCount > 0)
            {
                await _emailService.SendProcessingSummaryAsync(result.ProcessedCount, result.ErrorCount, errors);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during retry processing");
            await _emailService.SendErrorNotificationAsync("Document Retry Failed", 
                "A critical error occurred during document retry processing", ex);
            throw;
        }

        return result;
    }
}

public class ProcessingResult
{
    public int ProcessedCount { get; set; }
    public int ErrorCount { get; set; }
}