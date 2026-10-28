using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Settings.GetProducts;
using Application.Telemetry;
using Domain.Shared;
using Response = Contracts.Settings.GetProducts.Response;

namespace Application.Settings.UpdateProducts;

/// <summary>Switches the Open Food Facts lookup on or off.</summary>
/// <param name="OpenFoodFacts">Whether to look barcodes up there.</param>
/// <param name="Contact">The address for the User-Agent; without one, Culina's project page is sent.</param>
public sealed record UpdateProductSettingsCommand(bool OpenFoodFacts, string? Contact);

internal sealed class UpdateProductSettingsCommandHandler(
    ProductSettings settings,
    ISettingsStore<ProductSettings> store)
    : ICommandHandler<UpdateProductSettingsCommand, Response>
{
    private const int LongestContact = 200;

    public async Task<Result<Response>> Handle(UpdateProductSettingsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Settings.UpdateProducts");

        var contact = string.IsNullOrWhiteSpace(command.Contact) ? null : command.Contact.Trim();

        // The contact goes into a header: a line break would split it.
        if (contact is { Length: > LongestContact } || contact?.Any(char.IsControl) == true)
        {
            return tracked.Record(Result<Response>.Failure(new FieldError(
                "contact",
                SettingsErrors.InvalidValue.Code,
                $"A contact is one email or web address of at most {LongestContact} characters.")));
        }

        var updated = new ProductSettings { OpenFoodFacts = command.OpenFoodFacts, Contact = contact };
        var saved = await store.SaveAsync(updated, cancellationToken).ConfigureAwait(false);

        // Persist first, then mutate: a failed save must not change the running process.
        return tracked.Record(saved.Map(() =>
        {
            settings.CopyFrom(updated);

            return settings.ToResponse();
        }));
    }
}
