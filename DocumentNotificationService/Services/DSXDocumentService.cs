using System.ServiceModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DocumentNotificationService.Configuration;

namespace DocumentNotificationService.Services;

public class DSXDocumentService : IDisposable
{
    private readonly ILogger<DSXDocumentService> _logger;
    private readonly DSXServiceSettings _settings;
    private IDSXDocumentService? _client;
    private bool _disposed = false;

    public DSXDocumentService(ILogger<DSXDocumentService> logger, IOptions<AppSettings> options)
    {
        _logger = logger;
        _settings = options.Value.DSXService;
    }

    private IDSXDocumentService GetClient()
    {
        if (_client == null)
        {
            var binding = new BasicHttpBinding
            {
                MaxReceivedMessageSize = 10 * 1024 * 1024, // 10MB
                SendTimeout = _settings.Timeout,
                ReceiveTimeout = _settings.Timeout
            };

            var endpoint = new EndpointAddress(_settings.ServiceUrl);
            var factory = new ChannelFactory<IDSXDocumentService>(binding, endpoint);
            
            _client = factory.CreateChannel();
            _logger.LogInformation("Created DSX service client for endpoint: {Endpoint}", _settings.ServiceUrl);
        }

        return _client;
    }

    public async Task<List<DocumentInfo>> SearchDocumentsAsync(DateTime since, List<string> documentTypes)
    {
        try
        {
            _logger.LogInformation("Searching documents since {Since} for types: {DocumentTypes}", 
                since, string.Join(", ", documentTypes));

            var client = GetClient();
            var documents = new List<DocumentInfo>();
            
            var searchCriteria = new SearchCriteria
            {
                FromDate = since,
                MetadataFields = documentTypes.Select(dt => new MetadataSearchField
                {
                    FieldName = "Document Type",
                    Value = dt,
                    Operation = SearchOperation.Equals
                }).ToArray()
            };

            var searchRequest = new SearchWithResultsRequest
            {
                SearchCriteria = searchCriteria,
                MaxResults = _settings.PageSize
            };

            var searchResponse = await client.SearchWithResultsAsync(searchRequest);
            
            _logger.LogInformation("Initial search returned {Count} documents out of {Total}", 
                searchResponse.Results.Length, searchResponse.TotalCount);

            // Add initial results
            documents.AddRange(ConvertToDocumentInfo(searchResponse.Results));

            // Handle pagination if there are more results
            int processedCount = searchResponse.Results.Length;
            while (processedCount < searchResponse.TotalCount)
            {
                var getResultsRequest = new GetSearchResultsRequest
                {
                    SearchId = searchResponse.SearchId,
                    StartIndex = processedCount,
                    MaxResults = _settings.PageSize
                };

                var resultsResponse = await client.GetSearchResultsAsync(getResultsRequest);
                documents.AddRange(ConvertToDocumentInfo(resultsResponse.Results));
                
                processedCount += resultsResponse.Results.Length;
                
                _logger.LogInformation("Retrieved page, processed {Processed} of {Total} documents", 
                    processedCount, searchResponse.TotalCount);

                if (!resultsResponse.HasMore)
                    break;
            }

            _logger.LogInformation("Completed document search, found {Count} documents", documents.Count);
            return documents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching documents from DSX service");
            throw;
        }
    }

    private List<DocumentInfo> ConvertToDocumentInfo(DocumentSearchResult[] results)
    {
        var documents = new List<DocumentInfo>();

        foreach (var result in results)
        {
            try
            {
                var documentInfo = new DocumentInfo
                {
                    DocumentId = result.DocumentId,
                    Name = result.Name
                };

                // Extract metadata
                foreach (var metadata in result.Metadata)
                {
                    switch (metadata.Name.ToLowerInvariant())
                    {
                        case "document date":
                            if (DateTime.TryParse(metadata.Value, out var documentDate))
                                documentInfo.DocumentDate = documentDate;
                            break;
                        case "portfolio id":
                            documentInfo.PortfolioId = metadata.Value;
                            break;
                        case "document type":
                            documentInfo.DocumentType = metadata.Value;
                            break;
                        case "reference":
                            documentInfo.Reference = metadata.Value;
                            break;
                    }
                }

                // Use reference as name if name is empty
                if (string.IsNullOrEmpty(documentInfo.Name) && !string.IsNullOrEmpty(documentInfo.Reference))
                {
                    documentInfo.Name = documentInfo.Reference;
                }

                // Validate required fields
                if (string.IsNullOrEmpty(documentInfo.PortfolioId))
                {
                    _logger.LogWarning("Document {DocumentId} missing Portfolio ID, skipping", result.DocumentId);
                    continue;
                }

                documents.Add(documentInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting document result {DocumentId}", result.DocumentId);
            }
        }

        return documents;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_client is IClientChannel channel)
            {
                try
                {
                    if (channel.State == CommunicationState.Faulted)
                        channel.Abort();
                    else
                        channel.Close();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error closing DSX service client");
                    channel.Abort();
                }
            }

            _disposed = true;
        }
    }
}

public class DocumentInfo
{
    public string DocumentId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PortfolioId { get; set; } = string.Empty;
    public DateTime DocumentDate { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
}