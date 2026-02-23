using System.Data;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Vitaent.Infrastructure.Persistence.Seeding;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider services,
        ILogger logger,
        DatabaseInitializationState initializationState,
        CancellationToken cancellationToken = default)
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
                }

                var tenantsTableExists = await TenantsTableExistsAsync(dbContext, cancellationToken);
                if (!tenantsTableExists)
                {
                    const string message = "Database initialization failed: migrations did not create the tenants table.";
                    initializationState.MarkFailed(message);
                    logger.LogError(message);
                    return;
                }

                await DevelopmentSeed.SeedAsync(dbContext, cancellationToken);

                initializationState.MarkReady();
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
            catch (Exception ex)
            {
                var message = $"Database initialization failed: {ex.Message}";
                initializationState.MarkFailed(message);
                logger.LogError(ex, "Database initialization stopped due to a non-transient error.");
                return;
            }
        }

        const string timeoutMessage = "Database initialization failed after all retry attempts.";
        initializationState.MarkFailed(timeoutMessage);
        logger.LogError(timeoutMessage);
    }

    private static async Task<bool> TenantsTableExistsAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT to_regclass('public.tenants') IS NOT NULL;";
        var result = await command.ExecuteScalarAsync(cancellationToken);

        return result is bool exists && exists;
    }

    private static bool IsTransient(Exception exception)
    {
        if (exception is NpgsqlException || exception is SocketException || exception is TimeoutException)
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
}
