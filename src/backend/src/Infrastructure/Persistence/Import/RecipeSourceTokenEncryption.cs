using Application.Abstractions;
using Infrastructure.Assistance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence.Import;

/// <summary>
/// Encrypts connected-source tokens stored before encryption existed (migration 0027 marks them; SQL cannot reach the key ring).
/// Each row updates only while still plain, so restarts and concurrent instances never encrypt twice.
/// </summary>
/// <param name="scopeFactory">Makes the scope the database connection lives in.</param>
/// <param name="tokens">The protector source tokens are kept under.</param>
/// <param name="logger">Says how many were encrypted.</param>
internal sealed partial class RecipeSourceTokenEncryption(
    IServiceScopeFactory scopeFactory,
    [FromKeyedServices(SecretProtector.SourceTokens)] ISecretProtector tokens,
    ILogger<RecipeSourceTokenEncryption> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var scope = scopeFactory.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            var executor = scope.ServiceProvider.GetRequiredService<DbExecutor>();

            var plain = await executor.QueryAsync<PlainToken>(
                    "select id, secret from recipe_sources where not secret_protected;",
                    null,
                    cancellationToken)
                .ConfigureAwait(false);

            foreach (var row in plain)
            {
                await executor.ExecuteAsync(
                        """
                        update recipe_sources set secret = @secret, secret_protected = true
                        where id = @id and not secret_protected;
                        """,
                        new { id = row.Id, secret = tokens.Protect(row.Secret) },
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (plain.Count > 0)
            {
                LogEncrypted(logger, plain.Count);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        EventId = 1203,
        Level = LogLevel.Information,
        Message = "Encrypted {Count} connected-source tokens stored before tokens were encrypted")]
    private static partial void LogEncrypted(ILogger logger, int count);

    private sealed record PlainToken(Guid Id, string Secret);
}
