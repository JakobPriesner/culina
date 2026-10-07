using System.Globalization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Api;

/// <summary>
/// Writes the OpenAPI document to disk and exits (<c>make openapi</c>). The document comes from the endpoints of a started host,
/// so it needs the app's configuration and database; no request is served.
/// </summary>
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

        // Registered per document name; the key is the exposed version.
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

                // Ends in a newline per .editorconfig, or an editor's save becomes a diff the contract check fails on.
                await writer.WriteAsync('\n').ConfigureAwait(false);
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
