using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DocumentNotificationService.Configuration;
using DocumentNotificationService.Models;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DocumentNotificationService.Services;

public class CrmDynamicsService : IDisposable
{
    private readonly ILogger<CrmDynamicsService> _logger;
    private readonly CrmDynamicsSettings _settings;
    private ServiceClient? _serviceClient;
    private bool _disposed = false;

    public CrmDynamicsService(ILogger<CrmDynamicsService> logger, IOptions<AppSettings> options)
    {
        _logger = logger;
        _settings = options.Value.CrmDynamics;
    }

    private ServiceClient GetServiceClient()
    {
        if (_serviceClient == null || !_serviceClient.IsReady)
        {
            try
            {
                var connectionString = $"AuthType=ClientSecret;Url={_settings.ServiceUrl};ClientId={_settings.ClientId};ClientSecret={_settings.ClientSecret};";
                _serviceClient = new ServiceClient(connectionString);
                
                if (!_serviceClient.IsReady)
                {
                    var errorMessage = _serviceClient.LastException?.Message ?? "Unknown connection error";
                    throw new InvalidOperationException($"Failed to connect to CRM Dynamics: {errorMessage}");
                }
                
                _logger.LogInformation("Successfully connected to CRM Dynamics at {Url}", _settings.ServiceUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create CRM Dynamics service client");
                throw;
            }
        }
        
        return _serviceClient;
    }

    public async Task<List<PortfolioOwner>> GetPortfolioOwnersAsync(IEnumerable<string> portfolioIds)
    {
        var owners = new List<PortfolioOwner>();
        var portfolioIdList = portfolioIds.ToList();
        
        _logger.LogInformation("Retrieving owners for {Count} portfolios", portfolioIdList.Count);

        try
        {
            var client = GetServiceClient();
            
            // Process in batches to handle large datasets efficiently
            var batches = portfolioIdList.Chunk(_settings.BatchSize);
            
            foreach (var batch in batches)
            {
                var batchOwners = await GetPortfolioOwnersBatchAsync(client, batch);
                owners.AddRange(batchOwners);
                
                _logger.LogDebug("Processed batch of {BatchSize} portfolios, found {OwnersFound} owners", 
                    batch.Count(), batchOwners.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving portfolio owners from CRM Dynamics");
            throw;
        }

        _logger.LogInformation("Retrieved {OwnerCount} owners for {PortfolioCount} portfolios", 
            owners.Count, portfolioIdList.Count);
        
        return owners;
    }

    private async Task<List<PortfolioOwner>> GetPortfolioOwnersBatchAsync(ServiceClient client, IEnumerable<string> portfolioIds)
    {
        var owners = new List<PortfolioOwner>();
        var portfolioIdArray = portfolioIds.ToArray();
        
        try
        {
            // First, try to find contacts (private persons) as primary owners
            var contactQuery = new QueryExpression("contact")
            {
                ColumnSet = new ColumnSet("contactid", "firstname", "lastname", "emailaddress1", "new_portfolioid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("new_portfolioid", ConditionOperator.In, portfolioIdArray),
                        new ConditionExpression("statecode", ConditionOperator.Equal, 0) // Active
                    }
                }
            };

            var contactResults = await Task.Run(() => client.RetrieveMultiple(contactQuery));
            
            foreach (var contact in contactResults.Entities)
            {
                var portfolioId = contact.GetAttributeValue<string>("new_portfolioid");
                if (!string.IsNullOrEmpty(portfolioId))
                {
                    owners.Add(new PortfolioOwner
                    {
                        Id = contact.Id.ToString(),
                        Type = OwnerType.Contact,
                        PortfolioId = portfolioId,
                        FirstName = contact.GetAttributeValue<string>("firstname") ?? "",
                        LastName = contact.GetAttributeValue<string>("lastname") ?? "",
                        Name = $"{contact.GetAttributeValue<string>("firstname")} {contact.GetAttributeValue<string>("lastname")}".Trim(),
                        Email = contact.GetAttributeValue<string>("emailaddress1") ?? ""
                    });
                }
            }

            // Find remaining portfolios that don't have contact owners, and look for account owners
            var foundPortfolioIds = owners.Select(o => o.PortfolioId).ToHashSet();
            var remainingPortfolioIds = portfolioIdArray.Where(p => !foundPortfolioIds.Contains(p)).ToArray();

            if (remainingPortfolioIds.Any())
            {
                var accountQuery = new QueryExpression("account")
                {
                    ColumnSet = new ColumnSet("accountid", "name", "emailaddress1", "new_portfolioid", "new_contactpersonemail"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("new_portfolioid", ConditionOperator.In, remainingPortfolioIds),
                            new ConditionExpression("statecode", ConditionOperator.Equal, 0) // Active
                        }
                    }
                };

                var accountResults = await Task.Run(() => client.RetrieveMultiple(accountQuery));
                
                foreach (var account in accountResults.Entities)
                {
                    var portfolioId = account.GetAttributeValue<string>("new_portfolioid");
                    if (!string.IsNullOrEmpty(portfolioId))
                    {
                        var orgName = account.GetAttributeValue<string>("name") ?? "";
                        owners.Add(new PortfolioOwner
                        {
                            Id = account.Id.ToString(),
                            Type = OwnerType.Account,
                            PortfolioId = portfolioId,
                            Name = orgName,
                            OrganizationName = orgName,
                            Email = account.GetAttributeValue<string>("emailaddress1") ?? "",
                            ContactPersonEmail = account.GetAttributeValue<string>("new_contactpersonemail") ?? ""
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving batch of portfolio owners");
            throw;
        }

        return owners;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _serviceClient?.Dispose();
            _serviceClient = null;
            _disposed = true;
        }
    }
}