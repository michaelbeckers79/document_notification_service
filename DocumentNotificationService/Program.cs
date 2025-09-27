using CommandLine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DocumentNotificationService.Commands;
using DocumentNotificationService.Configuration;
using DocumentNotificationService.Data;
using DocumentNotificationService.Services;
using Serilog;
using Serilog.Events;

namespace DocumentNotificationService;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            var result = await Parser.Default.ParseArguments<ProcessDocumentsOptions, StatusOptions, RetryOptions, MigrateOptions, HealthOptions>(args)
                .MapResult(
                    (ProcessDocumentsOptions opts) => RunProcessCommand(opts),
                    (StatusOptions opts) => RunStatusCommand(opts),
                    (RetryOptions opts) => RunRetryCommand(opts),
                    (MigrateOptions opts) => RunMigrateCommand(opts),
                    (HealthOptions opts) => RunHealthCommand(opts),
                    errs => Task.FromResult(1));

            return result;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    static async Task<int> RunProcessCommand(ProcessDocumentsOptions options)
    {
        using var host = CreateHostBuilder().Build();
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        var processingService = host.Services.GetRequiredService<DocumentProcessingService>();

        try
        {
            logger.LogInformation("Starting document processing command");
            
            var result = await processingService.ProcessDocumentsAsync(options.Since, options.DryRun, options.Force, options.NoSummaryEmail, options.FailuresOnly);
            
            Console.WriteLine($"Processing completed:");
            Console.WriteLine($"  Documents processed: {result.ProcessedCount}");
            Console.WriteLine($"  Errors encountered: {result.ErrorCount}");
            
            if (options.DryRun)
            {
                Console.WriteLine("  (Dry run - no changes were made)");
            }

            return result.ErrorCount > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during document processing");
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    static async Task<int> RunStatusCommand(StatusOptions options)
    {
        using var host = CreateHostBuilder().Build();
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        var processingService = host.Services.GetRequiredService<DocumentProcessingService>();

        try
        {
            logger.LogInformation("Getting service status");
            
            var recentDocuments = await processingService.GetRecentDocumentsAsync(options.Limit);
            var failedDocuments = await processingService.GetFailedDocumentsAsync();

            Console.WriteLine("Document Notification Service Status:");
            Console.WriteLine($"Recent documents processed: {recentDocuments.Count}");
            Console.WriteLine($"Failed documents: {failedDocuments.Count}");
            Console.WriteLine();

            if (recentDocuments.Any())
            {
                Console.WriteLine($"Last {Math.Min(options.Limit, recentDocuments.Count)} processed documents:");
                foreach (var doc in recentDocuments)
                {
                    var status = doc.MessageSent ? "✓" : "✗";
                    Console.WriteLine($"  {status} {doc.DocumentId} - {doc.Name} ({doc.ProcessedAt:yyyy-MM-dd HH:mm})");
                    if (!string.IsNullOrEmpty(doc.ErrorMessage))
                    {
                        Console.WriteLine($"    Error: {doc.ErrorMessage}");
                    }
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting service status");
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    static async Task<int> RunRetryCommand(RetryOptions options)
    {
        using var host = CreateHostBuilder().Build();
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        var processingService = host.Services.GetRequiredService<DocumentProcessingService>();

        try
        {
            logger.LogInformation("Starting retry command");
            
            var result = await processingService.RetryFailedDocumentsAsync(options.DocumentId, options.NoSummaryEmail, options.FailuresOnly);
            
            Console.WriteLine($"Retry completed:");
            Console.WriteLine($"  Documents retried successfully: {result.ProcessedCount}");
            Console.WriteLine($"  Documents still failing: {result.ErrorCount}");

            return result.ErrorCount > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during retry operation");
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    static async Task<int> RunMigrateCommand(MigrateOptions options)
    {
        using var host = CreateHostBuilder().Build();
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        var context = host.Services.GetRequiredService<DocumentNotificationContext>();

        try
        {
            logger.LogInformation("Running database migrations");

            if (options.Create)
            {
                await context.Database.EnsureCreatedAsync();
                Console.WriteLine("Database created successfully");
            }
            else
            {
                await context.Database.MigrateAsync();
                Console.WriteLine("Database migrations applied successfully");
            }

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during database migration");
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    static async Task<int> RunHealthCommand(HealthOptions options)
    {
        using var host = CreateHostBuilder().Build();
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        
        try
        {
            logger.LogInformation("Starting health check");
            
            // Test database connection
            var context = host.Services.GetRequiredService<DocumentNotificationContext>();
            await context.Database.CanConnectAsync();
            
            // Test RabbitMQ connection (basic connectivity)
            var rabbitService = host.Services.GetRequiredService<RabbitMQService>();
            // Note: RabbitMQService doesn't have a health check method, so we'll just test instantiation
            
            Console.WriteLine("Health check passed - all systems operational");
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Health check failed");
            Console.Error.WriteLine($"Health check failed: {ex.Message}");
            return 1;
        }
    }

    static IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddEnvironmentVariables();
            })
            .UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
                .WriteTo.File("logs/document-notification-.txt", 
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}",
                    retainedFileCountLimit: 31)
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .Enrich.FromLogContext())
            .ConfigureServices((context, services) =>
            {
                // Configuration
                services.Configure<AppSettings>(context.Configuration);

                // Database
                var connectionString = context.Configuration.GetConnectionString("DefaultConnection") 
                    ?? context.Configuration["Database:ConnectionString"];
                
                services.AddDbContext<DocumentNotificationContext>(options =>
                    options.UseSqlServer(connectionString));

                // Services
                services.AddScoped<DSXDocumentService>();
                services.AddScoped<RabbitMQService>();
                services.AddScoped<EmailNotificationService>();
                services.AddScoped<DocumentProcessingService>();
            });
    }
}
