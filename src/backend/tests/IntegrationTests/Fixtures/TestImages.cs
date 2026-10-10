using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
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

    /// <summary>A valid TIFF: a format Culina does not accept.</summary>
    internal static byte[] Tiff(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        using var buffer = new MemoryStream();

        image.Save(buffer, new TiffEncoder());

        return buffer.ToArray();
    }

    /// <summary>A small photograph that says where it was taken.</summary>
    internal static byte[] LocatedPhotograph(int width = 64, int height = 48)
    {
        using var image = new Image<Rgba32>(width, height);
        using var buffer = new MemoryStream();

        var exif = new ExifProfile();

        exif.SetValue(ExifTag.GPSLatitudeRef, "N");
        exif.SetValue(ExifTag.GPSLatitude, [new Rational(49), new Rational(47), new Rational(0)]);

        image.Metadata.ExifProfile = exif;
        image.Save(buffer, new JpegEncoder());

        return buffer.ToArray();
    }
}
