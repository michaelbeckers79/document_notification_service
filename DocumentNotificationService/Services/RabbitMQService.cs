using System.Text;
using System.Security.Authentication;
using RabbitMQ.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DocumentNotificationService.Configuration;

namespace DocumentNotificationService.Services;

public class RabbitMQService : IDisposable
{
    private readonly ILogger<RabbitMQService> _logger;
    private readonly RabbitMQSettings _settings;
    private IConnection? _connection;
    private IChannel? _channel;
    private bool _disposed = false;

    public RabbitMQService(ILogger<RabbitMQService> logger, IOptions<AppSettings> options)
    {
        _logger = logger;
        _settings = options.Value.RabbitMQ;
    }

    private async Task<IChannel> GetChannelAsync()
    {
        if (_connection == null || !_connection.IsOpen)
        {
            await CreateConnectionAsync();
        }

        if (_channel == null || !_channel.IsOpen)
        {
            _channel = await _connection!.CreateChannelAsync();
            
            // Declare the exchange
            //await _channel.ExchangeDeclareAsync(
            //    exchange: _settings.Exchange,
            //    type: ExchangeType.Headers,
            //    durable: true);

            _logger.LogInformation("Created RabbitMQ channel and declared exchange: {Exchange}", _settings.Exchange);
        }

        return _channel;
    }

    private async Task CreateConnectionAsync()
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _settings.HostName,
                VirtualHost = _settings.VHost,
                Port = _settings.Port,
                UserName = _settings.UserName,
                Password = _settings.Password
            };

            if (_settings.UseSsl)
            {
                factory.Ssl = new SslOption
                {
                    Enabled = true,
                    ServerName = _settings.SslSettings.ServerName,
                    Version = SslProtocols.Tls12 | SslProtocols.Tls13
                };

                if (!string.IsNullOrEmpty(_settings.SslSettings.CertificatePath))
                {
                    factory.Ssl.CertPath = _settings.SslSettings.CertificatePath;
                    factory.Ssl.CertPassphrase = _settings.SslSettings.CertificatePassword;
                }
            }

            _connection = await factory.CreateConnectionAsync();
            _logger.LogInformation("Created RabbitMQ connection to {HostName}:{Port}", _settings.HostName, _settings.Port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create RabbitMQ connection");
            throw;
        }
    }

    public async Task PublishDocumentNotificationAsync(string portfolioId)
    {
        try
        {
            var channel = await GetChannelAsync();
            
            // Create message body by replacing placeholder in template
            var messageTemplate = await LoadMessageTemplateAsync();
            var messageBody = messageTemplate.Replace("{{Portfolio ID}}", portfolioId).Replace("{{TemplateId}}", _settings.TemplateId);

            var body = Encoding.UTF8.GetBytes(messageBody);

            // Create properties with required headers
            var properties = new BasicProperties
            {
                Persistent = true,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                Headers = new Dictionary<string, object?>
                {
                    ["$tenantid"] = _settings.TenantId,
                    ["$operation"] = _settings.Operation,
                    ["$application"] = _settings.Application
                }
            };

            await channel.BasicPublishAsync(
                exchange: _settings.Exchange,
                routingKey: _settings.RoutingKey,
                mandatory: true,
                basicProperties: properties,
                body: body);

            _logger.LogInformation("Published document notification for Portfolio ID: {PortfolioId}", portfolioId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish document notification for Portfolio ID: {PortfolioId}", portfolioId);
            throw;
        }
    }

    private async Task<string> LoadMessageTemplateAsync()
    {
        // Fallback to embedded template
        return @"<Communication xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns=""http://www.objectway.com/comm/request/communicationrequest"">
<Type>{{TemplateId}}</Type>
<EntityID>{{Portfolio ID}}</EntityID>
<ExternalData></ExternalData>
</Communication>";
    }

    public async Task PublishBatchDocumentNotificationsAsync(IEnumerable<string> portfolioIds)
    {
        var tasks = portfolioIds.Select(async portfolioId =>
        {
            try
            {
                await PublishDocumentNotificationAsync(portfolioId);
                return (PortfolioId: portfolioId, Success: true, Error: (string?)null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish notification for Portfolio ID: {PortfolioId}", portfolioId);
                return (PortfolioId: portfolioId, Success: false, Error: ex.Message);
            }
        });

        var results = await Task.WhenAll(tasks);
        
        var successful = results.Count(r => r.Success);
        var failed = results.Count(r => !r.Success);
        
        _logger.LogInformation("Batch notification complete: {Successful} successful, {Failed} failed", successful, failed);
        
        if (failed > 0)
        {
            var failedIds = results.Where(r => !r.Success).Select(r => r.PortfolioId);
            throw new InvalidOperationException($"Failed to publish notifications for Portfolio IDs: {string.Join(", ", failedIds)}");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                _channel?.Dispose();
                _connection?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing RabbitMQ resources");
            }

            _disposed = true;
        }
    }
}