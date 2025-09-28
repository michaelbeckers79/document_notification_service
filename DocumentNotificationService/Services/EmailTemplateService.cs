using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DocumentNotificationService.Configuration;
using DocumentNotificationService.Models;

namespace DocumentNotificationService.Services;

public class EmailTemplateService
{
    private readonly ILogger<EmailTemplateService> _logger;
    private readonly EmailSettings _settings;

    public EmailTemplateService(ILogger<EmailTemplateService> logger, IOptions<AppSettings> options)
    {
        _logger = logger;
        _settings = options.Value.Email;
    }

    public async Task SendDocumentNotificationEmailAsync(string portfolioId, PortfolioOwner owner, DocumentInfo document)
    {
        try
        {
            if (string.IsNullOrEmpty(owner.Email) && string.IsNullOrEmpty(owner.ContactPersonEmail))
            {
                _logger.LogWarning("No email address found for portfolio owner {OwnerId} in portfolio {PortfolioId}", 
                    owner.Id, portfolioId);
                return;
            }

            var template = await LoadEmailTemplateAsync();
            var emailContent = ReplaceTemplateVariables(template, portfolioId, owner, document);
            
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
            
            // Add recipient email
            var recipientEmail = !string.IsNullOrEmpty(owner.Email) ? owner.Email : owner.ContactPersonEmail;
            var recipientName = owner.Type == OwnerType.Contact 
                ? $"{owner.FirstName} {owner.LastName}".Trim() 
                : owner.OrganizationName;
            
            message.To.Add(new MailboxAddress(recipientName, recipientEmail));

            message.Subject = $"New Document Available - Portfolio {portfolioId}";

            var bodyBuilder = new BodyBuilder();
            bodyBuilder.HtmlBody = emailContent;
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

            _logger.LogInformation("Document notification email sent to {Email} for portfolio {PortfolioId}", 
                recipientEmail, portfolioId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send document notification email for portfolio {PortfolioId}", portfolioId);
            throw;
        }
    }

    private async Task<string> LoadEmailTemplateAsync()
    {
        if (!string.IsNullOrEmpty(_settings.TemplatePath) && File.Exists(_settings.TemplatePath))
        {
            try
            {
                return await File.ReadAllTextAsync(_settings.TemplatePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load email template from {TemplatePath}, using default template", _settings.TemplatePath);
            }
        }

        // Default template if no custom template is configured or if loading fails
        return GetDefaultEmailTemplate();
    }

    private string GetDefaultEmailTemplate()
    {
        return @"
<!DOCTYPE html>
<html>
<head>
    <title>New Document Available</title>
    <style>
        body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; }
        .header { background-color: #f8f9fa; padding: 20px; border-bottom: 2px solid #007bff; }
        .content { padding: 20px; }
        .footer { background-color: #f8f9fa; padding: 10px; font-size: 12px; color: #666; }
        .highlight { background-color: #fff3cd; padding: 10px; border-left: 4px solid #ffc107; margin: 15px 0; }
        .details { background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 15px 0; }
    </style>
</head>
<body>
    <div class=""header"">
        <h1>New Document Available</h1>
    </div>
    
    <div class=""content"">
        <p>Dear {{OwnerName}},</p>
        
        <div class=""highlight"">
            <strong>A new document is available for your portfolio {{PortfolioId}}.</strong>
        </div>
        
        <div class=""details"">
            <h3>Document Details:</h3>
            <ul>
                <li><strong>Document Name:</strong> {{DocumentName}}</li>
                <li><strong>Document Date:</strong> {{DocumentDate}}</li>
                <li><strong>Document ID:</strong> {{DocumentId}}</li>
                <li><strong>Portfolio ID:</strong> {{PortfolioId}}</li>
            </ul>
        </div>
        
        {{#if IsContact}}
        <p>This document has been prepared specifically for you as the portfolio holder.</p>
        {{else}}
        <p>This document has been prepared for your organization, {{OrganizationName}}.</p>
        {{/if}}
        
        <p>Please log in to your account to view and download the document.</p>
        
        <p>If you have any questions or need assistance, please don't hesitate to contact our support team.</p>
        
        <p>Best regards,<br>
        Document Notification Service</p>
    </div>
    
    <div class=""footer"">
        <p>This is an automated notification. Please do not reply to this email.</p>
        <p>Generated on {{NotificationDate}}</p>
    </div>
</body>
</html>";
    }

    private string ReplaceTemplateVariables(string template, string portfolioId, PortfolioOwner owner, DocumentInfo document)
    {
        var result = template
            .Replace("{{PortfolioId}}", portfolioId)
            .Replace("{{OwnerName}}", owner.Name)
            .Replace("{{DocumentName}}", document.Name)
            .Replace("{{DocumentDate}}", document.DocumentDate.ToString("yyyy-MM-dd"))
            .Replace("{{DocumentId}}", document.DocumentId)
            .Replace("{{NotificationDate}}", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"));

        // Handle conditional content based on owner type
        if (owner.Type == OwnerType.Contact)
        {
            result = result
                .Replace("{{#if IsContact}}", "")
                .Replace("{{else}}", "<!--")
                .Replace("{{/if}}", "-->")
                .Replace("{{OrganizationName}}", "");
        }
        else
        {
            result = result
                .Replace("{{#if IsContact}}", "<!--")
                .Replace("{{else}}", "-->")
                .Replace("{{/if}}", "")
                .Replace("{{OrganizationName}}", owner.OrganizationName);
        }

        // Clean up any remaining conditional markers
        result = result.Replace("<!--", "").Replace("-->", "");

        return result;
    }
}