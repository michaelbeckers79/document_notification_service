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

namespace DocumentNotificationService;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            var result = await Parser.Default.ParseArguments<ProcessDocumentsOptions, StatusOptions, RetryOptions, MigrateOptions>(args)
                .MapResult(
                    (ProcessDocumentsOptions opts) => RunProcessCommand(opts),
                    (StatusOptions opts) => RunStatusCommand(opts),
                    (RetryOptions opts) => RunRetryCommand(opts),
                    (MigrateOptions opts) => RunMigrateCommand(opts),
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
            
            var result = await processingService.ProcessDocumentsAsync(options.Since, options.DryRun);
            
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
            
            var result = await processingService.RetryFailedDocumentsAsync(options.DocumentId);
            
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

    static IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddEnvironmentVariables();
            })
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

                // Logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.AddDebug();
                });
            });
    }
}
