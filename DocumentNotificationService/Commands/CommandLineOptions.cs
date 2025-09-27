using CommandLine;

namespace DocumentNotificationService.Commands;

[Verb("process", HelpText = "Process new documents from DSX and send notifications")]
public class ProcessDocumentsOptions
{
    [Option('d', "dry-run", Required = false, HelpText = "Run without actually sending messages or updating database")]
    public bool DryRun { get; set; }

    [Option('f', "force", Required = false, HelpText = "Force processing even if no new documents found")]
    public bool Force { get; set; }

    [Option('s', "since", Required = false, HelpText = "Override the last query timestamp (ISO 8601 format)")]
    public DateTime? Since { get; set; }

    [Option("no-summary-email", Required = false, HelpText = "Disable summary email notification for this command")]
    public bool NoSummaryEmail { get; set; }

    [Option("failures-only", Required = false, HelpText = "Send summary email only if errors occur")]
    public bool FailuresOnly { get; set; }
}

[Verb("status", HelpText = "Check the status of the service and last processed documents")]
public class StatusOptions
{
    [Option('l', "limit", Required = false, Default = 10, HelpText = "Number of recent documents to show")]
    public int Limit { get; set; }
}

[Verb("retry", HelpText = "Retry failed document notifications")]
public class RetryOptions
{
    [Option('d', "document-id", Required = false, HelpText = "Retry specific document by ID")]
    public string? DocumentId { get; set; }

    [Option('a', "all", Required = false, HelpText = "Retry all failed documents")]
    public bool All { get; set; }

    [Option("no-summary-email", Required = false, HelpText = "Disable summary email notification for this command")]
    public bool NoSummaryEmail { get; set; }

    [Option("failures-only", Required = false, HelpText = "Send summary email only if errors occur")]
    public bool FailuresOnly { get; set; }
}

[Verb("migrate", HelpText = "Run database migrations")]
public class MigrateOptions
{
    [Option('c', "create", Required = false, HelpText = "Create the database if it doesn't exist")]
    public bool Create { get; set; }
}

[Verb("health", HelpText = "Check service health for Kubernetes probes")]
public class HealthOptions
{
    [Option('t', "timeout", Required = false, Default = 30, HelpText = "Health check timeout in seconds")]
    public int Timeout { get; set; }
}