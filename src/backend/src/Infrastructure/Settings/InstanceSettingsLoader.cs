using Application.Abstractions.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Settings;

/// <summary>
/// Loads every instance-settings group once, before the app serves traffic.
/// </summary>
/// <remarks>
/// Runs after the migration service, so the <c>settings</c> table exists. A
/// group with no stored row keeps its compiled-in defaults, which is what makes
/// a fresh instance work without any setup step.
/// </remarks>
/// <param name="scopeFactory">Creates the scope the stores live in.</param>
internal sealed class InstanceSettingsLoader(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var scope = scopeFactory.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            await LoadAsync<RegistrationSettings>(scope.ServiceProvider, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task LoadAsync<TSettings>(
        IServiceProvider services,
        CancellationToken cancellationToken)
        where TSettings : class, IInstanceSettings<TSettings>
    {
        var store = services.GetRequiredService<ISettingsStore<TSettings>>();
        var live = services.GetRequiredService<TSettings>();

        if (await store.LoadAsync(cancellationToken).ConfigureAwait(false) is { } stored)
        {
            live.CopyFrom(stored);
        }
    }
}
