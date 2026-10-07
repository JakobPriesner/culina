using Application.Abstractions;
using Domain.Assistance;

namespace Infrastructure.Assistance;

/// <summary>What the models this knows about cost, so a call can be priced.</summary>
/// <remarks>
/// Code, not configuration, so prices are comparable across instances: correcting one is a commit.
/// List prices in US dollars per million tokens as recorded on 2026-09-19; they go stale, so move that date when updating one.
/// An unknown model records tokens and no cost, never an invented number.
/// </remarks>
internal sealed class ModelPrices : IModelPrices
{
    private const decimal PerMillion = 1_000_000m;

    // What a million tokens in and a million tokens out cost.
    /// <param name="Input">Dollars per million input tokens.</param>
    /// <param name="Output">Dollars per million output tokens.</param>
    private readonly record struct Price(decimal Input, decimal Output);

    // Matched on a prefix, since providers append a date (<c>gpt-4o-mini-2024-07-18</c>) and exact matching would price pinned snapshots at nothing.
    private static readonly (string Prefix, Price Price)[] Text =
    [
        // Longest prefix first: gemini-3.1-flash-lite must not be read as gemini-3.1-flash.
        ("gemini-3.1-flash-lite", new Price(0.10m, 0.40m)),
        ("gemini-3.1-pro", new Price(1.25m, 10.00m)),
        ("gemini-3.1-flash", new Price(0.30m, 2.50m)),
        ("gemini-3-flash", new Price(0.30m, 2.50m)),
        ("gemini-3-pro", new Price(1.25m, 10.00m)),
        ("gemini-2.5-flash-lite", new Price(0.10m, 0.40m)),
        ("gemini-2.5-flash", new Price(0.30m, 2.50m)),
        ("gemini-2.5-pro", new Price(1.25m, 10.00m)),
        ("gpt-6-astra", new Price(1.25m, 10.00m)),
        ("gpt-4o-mini", new Price(0.15m, 0.60m)),
        ("gpt-4o", new Price(2.50m, 10.00m)),
        ("gpt-4.1-mini", new Price(0.40m, 1.60m)),
        ("gpt-4.1", new Price(2.00m, 8.00m))
    ];

    // What one picture costs, by model: per image (as both providers bill it), square at the middling quality this asks for.
    private static readonly (string Prefix, decimal Each)[] Pictures =
    [
        ("gpt-image-2.5-sunburst", 0.08m),
        ("gpt-image-2.5-flare", 0.04m),
        ("gpt-image-2", 0.04m),
        ("gpt-image-1-mini", 0.01m),
        ("gpt-image-1", 0.04m),
        ("gemini-3.1-flash-image", 0.039m),
        ("gemini-3-pro-image", 0.08m),
        ("gemini-2.5-flash-image", 0.039m),
        ("imagen-4", 0.04m)
    ];

    /// <summary>What a call came to, or null for a model with no price here. Ollama is zero, a known price, not unknown.</summary>
    /// <param name="provider">Which provider was used.</param>
    /// <param name="model">Which model was used.</param>
    /// <param name="inputTokens">What was sent.</param>
    /// <param name="outputTokens">What came back.</param>
    /// <param name="pictures">How many images were made.</param>
    public decimal? Of(
        AssistantKind provider,
        string model,
        int inputTokens,
        int outputTokens,
        int pictures)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (provider == AssistantKind.Ollama)
        {
            return 0m;
        }

        var name = model.Trim().ToLowerInvariant();

        if (pictures > 0)
        {
            return Match(Pictures, name) is { } each ? each * pictures : null;
        }

        if (Match(Text, name) is not { } price)
        {
            return null;
        }

        return ((inputTokens * price.Input) + (outputTokens * price.Output)) / PerMillion;
    }

    private static TPrice? Match<TPrice>((string Prefix, TPrice Price)[] table, string name)
        where TPrice : struct
    {
        foreach (var (prefix, price) in table)
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return price;
            }
        }

        return null;
    }
}
