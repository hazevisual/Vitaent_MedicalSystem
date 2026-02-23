using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Vitaent.Infrastructure.Persistence.Seeding;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken = default)
    {
        const int maxAttempts = 10;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var scope = services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                if (dbContext.Database.IsRelational())
                {
                    await dbContext.Database.MigrateAsync(cancellationToken);
                    await EnsureSchemaIsReadyAsync(dbContext, cancellationToken);
                }

                await DevelopmentSeed.SeedAsync(dbContext, cancellationToken);
                logger.LogInformation("Database migrations and seed completed successfully.");
                return;
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < maxAttempts)
            {
                var delaySeconds = Math.Min(30, (int)Math.Pow(2, attempt - 1));
                logger.LogWarning(ex,
                    "Database initialization attempt {Attempt}/{MaxAttempts} failed. Retrying in {DelaySeconds}s.",
                    attempt,
                    maxAttempts,
                    delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            }
        }

        throw new InvalidOperationException("Database initialization failed after all retry attempts.");
    }

    private static bool IsTransient(Exception exception)
    {
        if (exception is NpgsqlException || exception is SocketException)
        {
            return true;
        }

        if (exception is DbUpdateException dbUpdateException &&
            (dbUpdateException.InnerException is NpgsqlException || dbUpdateException.InnerException is SocketException))
        {
            return true;
        }

        if (exception.InnerException is not null)
        {
            return IsTransient(exception.InnerException);
        }

        return false;
    }

    private static async Task EnsureSchemaIsReadyAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        const string schemaCheckSql = """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_name IN ('tenants', 'tenant_branding', 'users', 'refresh_tokens', 'doctors', 'appointments');
            """;

        var tableCount = await dbContext.Database.SqlQueryRaw<int>(schemaCheckSql)
            .SingleAsync(cancellationToken);

        if (tableCount < 6)
        {
            throw new InvalidOperationException("Schema validation failed after migration. Expected tables were not found.");
        }
    }
}
