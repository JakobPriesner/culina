using System.Reflection;

namespace Infrastructure.Persistence.Migrations;

/// <summary>
/// Reads the migrations compiled into this assembly.
/// </summary>
/// <remarks>
/// Embedded rather than loose files, so a published container cannot be missing
/// a migration that the code assumes has run.
/// </remarks>
internal static class EmbeddedMigrations
{
    private const string ResourcePrefix = "Infrastructure.Persistence.Migrations.";

    internal static IReadOnlyList<SqlMigration> Load()
    {
        var assembly = typeof(EmbeddedMigrations).Assembly;

        var names = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Where(name => name.EndsWith(".sql", StringComparison.Ordinal))
            // File names are zero-padded (0001_, 0002_), so ordinal ordering is
            // numeric ordering and stays correct past migration 9.
            .OrderBy(name => name, StringComparer.Ordinal);

        return [.. names.Select(name => Read(assembly, name))];
    }

    private static SqlMigration Read(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded migration '{resourceName}' could not be opened.");
        using var reader = new StreamReader(stream);

        var version = resourceName[ResourcePrefix.Length..^".sql".Length];

        return new SqlMigration(version, reader.ReadToEnd());
    }
}
