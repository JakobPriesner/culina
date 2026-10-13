using Application.Abstractions.Settings;

namespace Infrastructure.Storage;

/// <summary>Renditions on a volume, under the image directory.</summary>
/// <param name="settings">Where the image directory is.</param>
internal sealed class FileSystemImageFiles(StorageSettings settings) : IImageFiles
{
    /// <summary>Writes beside the final path and moves into it, so the address only ever holds a whole file.</summary>
    public async Task WriteAsync(string key, byte[] bytes)
    {
        var path = PathFor(key);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (File.Exists(path))
        {
            // Content-addressed, so identical bytes are already there.
            return;
        }

        var temporary = $"{path}.{Guid.NewGuid():n}.tmp";

        await File.WriteAllBytesAsync(temporary, bytes, CancellationToken.None).ConfigureAwait(false);

        try
        {
            File.Move(temporary, path, overwrite: false);
        }
        catch (IOException) when (File.Exists(path))
        {
            // A concurrent store of the same photo moved in first.
            File.Delete(temporary);
        }
    }

    public async Task<bool> CopyToAsync(string key, Stream destination, CancellationToken cancellationToken)
    {
        var path = PathFor(key);

        if (!File.Exists(path))
        {
            return false;
        }

        var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        await using (file.ConfigureAwait(false))
        {
            await file.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        var path = PathFor(key);

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string PathFor(string key) => Path.Combine(settings.ImagePath, key);
}
