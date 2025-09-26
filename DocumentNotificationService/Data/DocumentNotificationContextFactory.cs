using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DocumentNotificationService.Data;

public class DocumentNotificationContextFactory : IDesignTimeDbContextFactory<DocumentNotificationContext>
{
    public DocumentNotificationContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? configuration["Database:ConnectionString"];

        var optionsBuilder = new DbContextOptionsBuilder<DocumentNotificationContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new DocumentNotificationContext(optionsBuilder.Options);
    }
}