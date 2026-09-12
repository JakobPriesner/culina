using System.Globalization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Api;

/// <summary>
/// Writes the OpenAPI document to disk and exits.
/// </summary>
/// <remarks>
/// <para>
/// An explicit step (<c>make openapi</c>) rather than an MSBuild target,
/// because the document is produced by enumerating the endpoints of a
/// <em>started</em> host — building one is not enough, and neither is
/// constructing the pipeline by hand.
/// </para>
/// <para>
/// Starting the host means the export needs the same configuration and the same
/// database the app needs, which is why <c>make openapi</c> brings the database
/// up first. The app starts, the document is read, the app stops, and no
/// request is ever served.
/// </para>
/// </remarks>
internal static class OpenApiExport
{
    internal const string Flag = "--export-openapi";

    private const string DocumentName = "v1";

    internal static bool Requested(string[] args) => args.Contains(Flag, StringComparer.Ordinal);

    internal static async Task WriteAsync(WebApplication app, string[] args)
    {
        var path = OutputPath(args);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await app.StartAsync().ConfigureAwait(false);

        // Registered per document name, so the key is the version this
        // application exposes.
        var provider = app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(DocumentName);
        var document = await provider.GetOpenApiDocumentAsync().ConfigureAwait(false);

        await app.StopAsync().ConfigureAwait(false);

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
