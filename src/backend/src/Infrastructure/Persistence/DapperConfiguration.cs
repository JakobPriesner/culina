using Dapper;

namespace Infrastructure.Persistence;

/// <summary>
/// The one-time Dapper setup, applied at startup.
/// </summary>
internal static class DapperConfiguration
{
    internal static void Apply() =>
        // PostgreSQL columns are snake_case and C# properties are PascalCase.
        // Matching them here removes an `as "DisplayName"` alias from every
        // column of every query, which is the kind of noise that eventually
        // gets one column wrong.
        DefaultTypeMap.MatchNamesWithUnderscores = true;
}
