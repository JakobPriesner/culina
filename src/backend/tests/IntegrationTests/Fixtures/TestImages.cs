using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace IntegrationTests.Fixtures;

/// <summary>Images to upload.</summary>
internal static class TestImages
{
    /// <summary>A blank PNG. Its size decides its content hash.</summary>
    internal static byte[] Png(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        using var buffer = new MemoryStream();

        image.Save(buffer, new PngEncoder());

        return buffer.ToArray();
    }
}
