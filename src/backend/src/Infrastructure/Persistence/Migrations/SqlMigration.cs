using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Persistence.Migrations;

/// <summary>
/// One forward-only schema change.
/// </summary>
/// <param name="Version">The ordering key, taken from the file name.</param>
/// <param name="Sql">The statements to run.</param>
internal sealed record SqlMigration(string Version, string Sql)
{
    /// <summary>
    /// Fingerprints the statements, so an already-applied migration that has
    /// since been edited can be detected rather than silently ignored.
    /// </summary>
    internal string Checksum { get; } =
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Sql)));
}
