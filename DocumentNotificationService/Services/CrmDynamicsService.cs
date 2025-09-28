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
                // Use OAuth for on-premise CRM Dynamics instance
                var connectionString = $"AuthType=OAuth;Url={_settings.ServiceUrl};ClientId={_settings.ClientId};ClientSecret={_settings.ClientSecret};RedirectUri=http://localhost;LoginPrompt=Never;";
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
            // Query the ofs_portfolio entity using ofs_externalid to find portfolios and their primary owners
            var portfolioQuery = new QueryExpression("ofs_portfolio")
            {
                ColumnSet = new ColumnSet("ofs_portfolioid", "ofs_externalid", "ofs_primaryowner"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("ofs_externalid", ConditionOperator.In, portfolioIdArray),
                        new ConditionExpression("statecode", ConditionOperator.Equal, 0) // Active
                    }
                },
                // Add link to the primary owner to get owner details
                LinkEntities =
                {
                    new LinkEntity
                    {
                        LinkFromEntityName = "ofs_portfolio",
                        LinkFromAttributeName = "ofs_primaryowner",
                        LinkToEntityName = "contact",
                        LinkToAttributeName = "contactid",
                        JoinOperator = JoinOperator.LeftOuter,
                        EntityAlias = "primarycontact",
                        Columns = new ColumnSet("contactid", "firstname", "lastname", "emailaddress1")
                    },
                    new LinkEntity
                    {
                        LinkFromEntityName = "ofs_portfolio",
                        LinkFromAttributeName = "ofs_primaryowner",
                        LinkToEntityName = "account",
                        LinkToAttributeName = "accountid",
                        JoinOperator = JoinOperator.LeftOuter,
                        EntityAlias = "primaryaccount",
                        Columns = new ColumnSet("accountid", "name", "emailaddress1")
                    }
                }
            };

            var portfolioResults = await Task.Run(() => client.RetrieveMultiple(portfolioQuery));
            
            foreach (var portfolio in portfolioResults.Entities)
            {
                var externalId = portfolio.GetAttributeValue<string>("ofs_externalid");
                if (string.IsNullOrEmpty(externalId))
                    continue;

                var primaryOwnerRef = portfolio.GetAttributeValue<EntityReference>("ofs_primaryowner");
                if (primaryOwnerRef == null)
                    continue;

                PortfolioOwner owner = null;

                // Check if primary owner is a contact (private person)
                if (primaryOwnerRef.LogicalName == "contact")
                {
                    var contactFirstName = portfolio.GetAttributeValue<AliasedValue>("primarycontact.firstname")?.Value as string ?? "";
                    var contactLastName = portfolio.GetAttributeValue<AliasedValue>("primarycontact.lastname")?.Value as string ?? "";
                    var contactEmail = portfolio.GetAttributeValue<AliasedValue>("primarycontact.emailaddress1")?.Value as string ?? "";

                    owner = new PortfolioOwner
                    {
                        Id = primaryOwnerRef.Id.ToString(),
                        Type = OwnerType.Contact,
                        PortfolioId = externalId,
                        FirstName = contactFirstName,
                        LastName = contactLastName,
                        Name = $"{contactFirstName} {contactLastName}".Trim(),
                        Email = contactEmail
                    };
                }
                // Check if primary owner is an account (organization)
                else if (primaryOwnerRef.LogicalName == "account")
                {
                    var accountName = portfolio.GetAttributeValue<AliasedValue>("primaryaccount.name")?.Value as string ?? "";
                    var accountEmail = portfolio.GetAttributeValue<AliasedValue>("primaryaccount.emailaddress1")?.Value as string ?? "";

                    owner = new PortfolioOwner
                    {
                        Id = primaryOwnerRef.Id.ToString(),
                        Type = OwnerType.Account,
                        PortfolioId = externalId,
                        Name = accountName,
                        OrganizationName = accountName,
                        Email = accountEmail,
                        ContactPersonEmail = accountEmail
                    };
                }

                if (owner != null)
                {
                    owners.Add(owner);
                    _logger.LogDebug("Found {OwnerType} owner {OwnerName} for portfolio {PortfolioId}", 
                        owner.Type, owner.Name, externalId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving batch of portfolio owners from ofs_portfolio entity");
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