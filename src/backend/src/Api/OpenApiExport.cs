using System.Globalization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Api;

/// <summary>
/// Writes the OpenAPI document to disk and exits.
/// </summary>
/// <remarks>
/// <para>
/// An explicit step (<c>dotnet run -- --export-openapi</c>, or
/// <c>make api</c>) rather than a build target. The build-time generator starts
/// the application to enumerate its endpoints, and this application refuses to
/// start without valid configuration — which is behaviour worth keeping, so the
/// export works around it instead of weakening it.
/// </para>
/// <para>
/// The placeholder configuration below exists only on this path. Nothing is
/// connected to and no request is served: the host is built, the document is
/// read from it, and the process exits.
/// </para>
/// </remarks>
internal static class OpenApiExport
{
    internal const string Flag = "--export-openapi";

    private const string DocumentName = "v1";

    internal static bool Requested(string[] args) => args.Contains(Flag, StringComparer.Ordinal);

    internal static IReadOnlyDictionary<string, string?> PlaceholderConfiguration()
    {
        var scratch = Path.Combine(Path.GetTempPath(), "culina-openapi-export");

        return new Dictionary<string, string?>
        {
            ["Database:Host"] = "openapi-export",
            ["Database:Name"] = "openapi-export",
            ["Database:Username"] = "openapi-export",
            ["Database:Password"] = "openapi-export",
            ["Storage:ImagePath"] = Path.Combine(scratch, "images"),
            ["Storage:DataProtectionKeyPath"] = Path.Combine(scratch, "keys")
        };
    }

    internal static async Task WriteAsync(WebApplication app, string[] args)
    {
        var path = OutputPath(args);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Registered per document name, so the key is the version this
        // application exposes.
        var provider = app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(DocumentName);
        var document = await provider.GetOpenApiDocumentAsync().ConfigureAwait(false);

        var stream = File.Create(path);
        await using (stream.ConfigureAwait(false))
        {
            var writer = new StreamWriter(stream);

            await using (writer.ConfigureAwait(false))
            {
                document.SerializeAsV3(new OpenApiJsonWriter(writer));
                await writer.FlushAsync().ConfigureAwait(false);
            }
        }

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Wrote OpenAPI document to {path}"));
    }

    private static string OutputPath(string[] args)
    {
        var index = Array.IndexOf(args, Flag);

        return index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith('-')
            ? Path.GetFullPath(args[index + 1])
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../openapi/Api.json"));
    }
}
