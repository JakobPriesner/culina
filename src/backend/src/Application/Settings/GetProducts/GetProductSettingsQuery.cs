using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Telemetry;
using Domain.Shared;
using Response = Contracts.Settings.GetProducts.Response;

namespace Application.Settings.GetProducts;

/// <summary>Reads whether this server looks barcodes up at Open Food Facts.</summary>
public sealed record GetProductSettingsQuery;

internal sealed class GetProductSettingsQueryHandler(ProductSettings settings)
    : IQueryHandler<GetProductSettingsQuery, Response>
{
    public Task<Result<Response>> Handle(GetProductSettingsQuery query, CancellationToken cancellationToken)
    {
        using var tracked = UseCaseActivity.Start("Settings.GetProducts");

        return Task.FromResult(tracked.Record(Result<Response>.Success(settings.ToResponse())));
    }
}

/// <summary>The live product settings to their wire shape.</summary>
internal static class ProductSettingsMappings
{
    internal static Response ToResponse(this ProductSettings settings) =>
        new() { OpenFoodFacts = settings.OpenFoodFacts, Contact = settings.Contact };
}
