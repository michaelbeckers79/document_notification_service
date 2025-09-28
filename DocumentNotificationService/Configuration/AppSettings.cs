namespace DocumentNotificationService.Configuration;

public class AppSettings
{
    public DSXServiceSettings DSXService { get; set; } = new();
    public DatabaseSettings Database { get; set; } = new();
    public RabbitMQSettings RabbitMQ { get; set; } = new();
    public EmailSettings Email { get; set; } = new();
    public CrmDynamicsSettings CrmDynamics { get; set; } = new();
}

public class DSXServiceSettings
{
    public string ServiceUrl { get; set; } = string.Empty;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
    public List<string> DocumentTypes { get; set; } = new();
    public int PageSize { get; set; } = 50;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class DatabaseSettings
{
    public string ConnectionString { get; set; } = string.Empty;
}

public class RabbitMQSettings
{
    public string HostName { get; set; } = string.Empty;
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public string RoutingKey { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = false;
    public SslSettings SslSettings { get; set; } = new();
    public string TenantId { get; set; } = "jmfinn";
    public string Operation { get; set; } = "comm:communication";
    public string Application { get; set; } = "thrd:dsx-service";
    public string VHost { get; set; } = "/";
    public string TemplateId { get; set; } = string.Empty;
}

public class SslSettings
{
    public string ServerName { get; set; } = string.Empty;
    public string CertificatePath { get; set; } = string.Empty;
    public string CertificatePassword { get; set; } = string.Empty;
}

public class EmailSettings
{
    public string SmtpServer { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public List<string> Recipients { get; set; } = new();
    public bool SendSummaryEmail { get; set; } = true;
    public bool SendFailuresOnly { get; set; } = false;
    public string TemplatePath { get; set; } = string.Empty;
    public bool UseEmailNotification { get; set; } = false;
}

public class CrmDynamicsSettings
{
    public string ServiceUrl { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
    public int BatchSize { get; set; } = 50;
}