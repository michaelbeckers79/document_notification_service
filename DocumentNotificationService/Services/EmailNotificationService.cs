using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DocumentNotificationService.Configuration;

namespace DocumentNotificationService.Services;

public class EmailNotificationService
{
    private readonly ILogger<EmailNotificationService> _logger;
    private readonly EmailSettings _settings;

    public EmailNotificationService(ILogger<EmailNotificationService> logger, IOptions<AppSettings> options)
    {
        _logger = logger;
        _settings = options.Value.Email;
    }

    public async Task SendErrorNotificationAsync(string subject, string errorDetails, Exception? exception = null)
    {
        try
        {
            if (!_settings.Recipients.Any())
            {
                _logger.LogWarning("No email recipients configured, skipping error notification");
                return;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
            
            foreach (var recipient in _settings.Recipients)
            {
                message.To.Add(new MailboxAddress("", recipient));
            }

            message.Subject = $"[Document Notification Service] {subject}";

            var bodyBuilder = new BodyBuilder();
            bodyBuilder.HtmlBody = CreateErrorEmailBody(subject, errorDetails, exception);
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            
            var secureSocketOptions = _settings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
            await client.ConnectAsync(_settings.SmtpServer, _settings.SmtpPort, secureSocketOptions);

            if (!string.IsNullOrEmpty(_settings.UserName))
            {
                await client.AuthenticateAsync(_settings.UserName, _settings.Password);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Error notification email sent to {Recipients}", string.Join(", ", _settings.Recipients));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send error notification email");
        }
    }

    public async Task SendProcessingSummaryAsync(int processedCount, int errorCount, List<string> errors, bool? overrideNoSummaryEmail = null, bool? overrideFailuresOnly = null)
    {
        try
        {
            // Check if summary email should be sent based on configuration and overrides
            var sendSummaryEmail = overrideNoSummaryEmail.HasValue ? !overrideNoSummaryEmail.Value : _settings.SendSummaryEmail;
            if (!sendSummaryEmail)
            {
                _logger.LogInformation("Summary email sending is disabled, skipping notification");
                return;
            }

            // Check if we should send only on failures
            var failuresOnly = overrideFailuresOnly ?? _settings.SendFailuresOnly;
            if (failuresOnly && errorCount == 0)
            {
                _logger.LogInformation("Failures-only mode is enabled and no errors occurred, skipping summary notification");
                return;
            }

            if (!_settings.Recipients.Any())
            {
                _logger.LogInformation("No email recipients configured, skipping summary notification");
                return;
            }

            var subject = errorCount > 0 
                ? $"Document Processing Complete with {errorCount} Errors"
                : "Document Processing Complete";

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
            
            foreach (var recipient in _settings.Recipients)
            {
                message.To.Add(new MailboxAddress("", recipient));
            }

            message.Subject = $"[Document Notification Service] {subject}";

            var bodyBuilder = new BodyBuilder();
            bodyBuilder.HtmlBody = CreateSummaryEmailBody(processedCount, errorCount, errors);
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            
            var secureSocketOptions = _settings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
            await client.ConnectAsync(_settings.SmtpServer, _settings.SmtpPort, secureSocketOptions);

            if (!string.IsNullOrEmpty(_settings.UserName))
            {
                await client.AuthenticateAsync(_settings.UserName, _settings.Password);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Processing summary email sent to {Recipients}", string.Join(", ", _settings.Recipients));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send processing summary email");
        }
    }

    private string CreateErrorEmailBody(string subject, string errorDetails, Exception? exception)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
        
        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <title>Document Notification Service Error</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        .header {{ background-color: #d32f2f; color: white; padding: 15px; }}
        .content {{ padding: 20px; border: 1px solid #ddd; }}
        .error-box {{ background-color: #ffebee; border: 1px solid #e57373; padding: 15px; margin: 10px 0; }}
        .timestamp {{ color: #666; font-size: 0.9em; }}
    </style>
</head>
<body>
    <div class='header'>
        <h2>Document Notification Service Error</h2>
    </div>
    <div class='content'>
        <h3>{subject}</h3>
        <p class='timestamp'>Occurred at: {timestamp}</p>
        
        <div class='error-box'>
            <h4>Error Details:</h4>
            <pre>{System.Net.WebUtility.HtmlEncode(errorDetails)}</pre>
        </div>";

        if (exception != null)
        {
            html += $@"
        <div class='error-box'>
            <h4>Exception Details:</h4>
            <p><strong>Type:</strong> {exception.GetType().Name}</p>
            <p><strong>Message:</strong> {System.Net.WebUtility.HtmlEncode(exception.Message)}</p>
            <pre><strong>Stack Trace:</strong>
{System.Net.WebUtility.HtmlEncode(exception.StackTrace ?? "Not available")}</pre>
        </div>";
        }

        html += @"
        <p>Please investigate and resolve this issue promptly.</p>
    </div>
</body>
</html>";

        return html;
    }

    private string CreateSummaryEmailBody(int processedCount, int errorCount, List<string> errors)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
        var statusColor = errorCount > 0 ? "#d32f2f" : "#2e7d32";
        var statusText = errorCount > 0 ? "Completed with Errors" : "Completed Successfully";
        
        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <title>Document Processing Summary</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        .header {{ background-color: {statusColor}; color: white; padding: 15px; }}
        .content {{ padding: 20px; border: 1px solid #ddd; }}
        .summary-box {{ background-color: #f5f5f5; border: 1px solid #ddd; padding: 15px; margin: 10px 0; }}
        .error-box {{ background-color: #ffebee; border: 1px solid #e57373; padding: 15px; margin: 10px 0; }}
        .timestamp {{ color: #666; font-size: 0.9em; }}
        .metric {{ font-size: 1.2em; font-weight: bold; margin: 5px 0; }}
    </style>
</head>
<body>
    <div class='header'>
        <h2>Document Processing Summary - {statusText}</h2>
    </div>
    <div class='content'>
        <p class='timestamp'>Completed at: {timestamp}</p>
        
        <div class='summary-box'>
            <h3>Processing Statistics:</h3>
            <div class='metric'>Documents Processed: {processedCount}</div>
            <div class='metric'>Errors Encountered: {errorCount}</div>
        </div>";

        if (errors.Any())
        {
            html += @"
        <div class='error-box'>
            <h4>Error Details:</h4>
            <ul>";
            
            foreach (var error in errors)
            {
                html += $"<li>{System.Net.WebUtility.HtmlEncode(error)}</li>";
            }
            
            html += @"
            </ul>
        </div>";
        }

        html += @"
        <p>This is an automated notification from the Document Notification Service.</p>
    </div>
</body>
</html>";

        return html;
    }
}