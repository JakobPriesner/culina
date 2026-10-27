using System.Buffers.Binary;
using System.Security.Cryptography;
using SkiaSharp;

namespace IntegrationTests.Fixtures;

/// <summary>Images to upload, and what to read back from the ones served.</summary>
/// <remarks>Skia encodes only PNG, JPEG and WebP and writes no metadata, so a TIFF, a GIF and EXIF are written out byte by byte.</remarks>
internal static class TestImages
{
    /// <summary>A blank PNG. Its size decides its content hash.</summary>
    internal static byte[] Png(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);

        // Skia does not clear what it allocates; without this, two uploads of one size would differ.
        bitmap.Erase(SKColors.Transparent);

        return Encode(bitmap, SKEncodedImageFormat.Png);
    }

    /// <summary>A PNG of noise, so its renditions are large enough that writing one takes a while.</summary>
    internal static byte[] NoisyPng(int width, int height)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        var pixels = new byte[info.BytesSize];

        RandomNumberGenerator.Fill(pixels);

        using var image = SKImage.FromPixelCopy(info, pixels);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);

        return encoded.ToArray();
    }

    /// <summary>A PNG with 16 bits a channel, the depth that would double what a decoder holds per pixel.</summary>
    internal static byte[] DeepPng(int width, int height)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba16161616, SKAlphaType.Unpremul));

        bitmap.Erase(SKColors.Orange);

        var png = Encode(bitmap, SKEncodedImageFormat.Png);

        // The IHDR's bit depth, so the test cannot pass on an 8-bit PNG by accident.
        Assert.Equal(16, png[24]);

        return png;
    }

    /// <summary>A valid TIFF, 8-bit grey and uncompressed: a format Culina does not accept.</summary>
    internal static byte[] Tiff(int width, int height)
    {
        const int Entries = 8;
        const int PixelsAt = 8 + 2 + (Entries * 12) + 4;

        var tiff = new byte[PixelsAt + (width * height)];
        var span = tiff.AsSpan();

        "II*\0"u8.CopyTo(span);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], 8);
        BinaryPrimitives.WriteUInt16LittleEndian(span[8..], Entries);

        ReadOnlySpan<(ushort Tag, ushort Type, uint Value)> entries =
        [
            (0x0100, 4, (uint)width), // ImageWidth
            (0x0101, 4, (uint)height), // ImageLength
            (0x0102, 3, 8), // BitsPerSample
            (0x0103, 3, 1), // Compression: none
            (0x0106, 3, 1), // PhotometricInterpretation: black is zero
            (0x0111, 4, PixelsAt), // StripOffsets
            (0x0116, 4, (uint)height), // RowsPerStrip: one strip
            (0x0117, 4, (uint)(width * height)) // StripByteCounts
        ];

        for (var i = 0; i < entries.Length; i++)
        {
            var entry = span[(10 + (i * 12))..];

            BinaryPrimitives.WriteUInt16LittleEndian(entry, entries[i].Tag);
            BinaryPrimitives.WriteUInt16LittleEndian(entry[2..], entries[i].Type);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[4..], 1);

            if (entries[i].Type == 3)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(entry[8..], (ushort)entries[i].Value);
            }
            else
            {
                BinaryPrimitives.WriteUInt32LittleEndian(entry[8..], entries[i].Value);
            }
        }

        return tiff;
    }

    /// <summary>A small photograph that says where it was taken: 49° 47′ N, in an EXIF GPS block.</summary>
    internal static byte[] LocatedPhotograph(int width = 64, int height = 48)
    {
        var exif = new byte[8 + 2 + 12 + 4 + 2 + 24 + 4 + 24];
        var span = exif.AsSpan();

        Header(span);

        // IFD0: only the pointer to the GPS block.
        BinaryPrimitives.WriteUInt16LittleEndian(span[8..], 1);
        Entry(span[10..], 0x8825, 4, 1, 26);

        // The GPS block: GPSLatitudeRef "N" inline, GPSLatitude as three rationals after it.
        BinaryPrimitives.WriteUInt16LittleEndian(span[26..], 2);
        Entry(span[28..], 0x0001, 2, 2, 'N');
        Entry(span[40..], 0x0002, 5, 3, 56);

        for (var i = 0; i < 3; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(span[(56 + (i * 8))..], i switch { 0 => 49u, 1 => 47u, _ => 0u });
            BinaryPrimitives.WriteUInt32LittleEndian(span[(60 + (i * 8))..], 1);
        }

        return JpegWithExif(width, height, exif);
    }

    /// <summary>A photograph stored as a camera does: the pixels as the sensor read them, plus the EXIF tag to turn them.</summary>
    internal static byte[] TaggedPhotograph(int width, int height, ushort orientation)
    {
        var exif = new byte[8 + 2 + 12 + 4];
        var span = exif.AsSpan();

        Header(span);
        BinaryPrimitives.WriteUInt16LittleEndian(span[8..], 1);
        Entry(span[10..], 0x0112, 3, 1, orientation);

        return JpegWithExif(width, height, exif);
    }

    /// <summary>A 1x1 GIF of two frames, red then green: decoding anything but the first would show green.</summary>
    internal static byte[] AnimatedGif() =>
    [
        .. "GIF89a"u8,
        1, 0, 1, 0, // 1x1
        0x91, 0, 0, // a global table of four colours
        255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0,
        .. Frame(0),
        .. Frame(1),
        0x3B
    ];

    /// <summary>The kinds of metadata an encoded JPEG or WebP still carries: EXIF, XMP, IPTC or ICC.</summary>
    internal static List<string> MetadataIn(byte[] encoded)
    {
        List<string> found = [];

        if (encoded is [0xFF, 0xD8, ..])
        {
            // JPEG: marker segments up to the start of the scan.
            for (var at = 2; at + 4 <= encoded.Length && encoded[at] == 0xFF && encoded[at + 1] != 0xDA;)
            {
                var length = BinaryPrimitives.ReadUInt16BigEndian(encoded.AsSpan(at + 2));
                var segment = encoded.AsSpan(at + 4, length - 2);

                found.AddRange(encoded[at + 1] switch
                {
                    0xE1 when segment.StartsWith("Exif\0\0"u8) => ["EXIF"],
                    0xE1 when segment.StartsWith("http://ns.adobe.com/xap/1.0/"u8) => ["XMP"],
                    0xE2 when segment.StartsWith("ICC_PROFILE"u8) => ["ICC"],
                    0xED => ["IPTC"],
                    _ => []
                });

                at += 2 + length;
            }

            return found;
        }

        Assert.True(encoded.AsSpan(8, 4).SequenceEqual("WEBP"u8), "Neither a JPEG nor a WebP.");

        // WebP: RIFF chunks, each padded to an even length.
        for (var at = 12; at + 8 <= encoded.Length;)
        {
            var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(at + 4));

            found.AddRange(System.Text.Encoding.ASCII.GetString(encoded, at, 4) switch
            {
                "EXIF" => ["EXIF"],
                "XMP " => ["XMP"],
                "ICCP" => ["ICC"],
                _ => []
            });

            at += 8 + size + (size % 2);
        }

        return found;
    }

    /// <summary>The size of an encoded image, read from its header.</summary>
    internal static SKSizeI SizeOf(byte[] encoded)
    {
        using var data = SKData.CreateCopy(encoded);
        using var codec = SKCodec.Create(data);

        return new SKSizeI(codec.Info.Width, codec.Info.Height);
    }

    private static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format)
    {
        using var encoded = bitmap.Encode(format, 90);

        return encoded.ToArray();
    }

    /// <summary>A grey JPEG with an APP1 EXIF segment right after the start-of-image marker, where cameras put it.</summary>
    private static byte[] JpegWithExif(int width, int height, byte[] exif)
    {
        using var bitmap = new SKBitmap(width, height);

        bitmap.Erase(SKColors.Gray);

        var jpeg = Encode(bitmap, SKEncodedImageFormat.Jpeg);
        var length = new byte[2];

        BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)(2 + 6 + exif.Length));

        return [0xFF, 0xD8, 0xFF, 0xE1, .. length, .. "Exif\0\0"u8, .. exif, .. jpeg.AsSpan(2)];
    }

    /// <summary>A little-endian TIFF header whose first directory follows it.</summary>
    private static void Header(Span<byte> exif)
    {
        "II*\0"u8.CopyTo(exif);
        BinaryPrimitives.WriteUInt32LittleEndian(exif[4..], 8);
    }

    /// <summary>One 12-byte directory entry; a value of four bytes or fewer sits in place of the offset.</summary>
    private static void Entry(Span<byte> at, ushort tag, ushort type, uint count, uint value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(at, tag);
        BinaryPrimitives.WriteUInt16LittleEndian(at[2..], type);
        BinaryPrimitives.WriteUInt32LittleEndian(at[4..], count);

        if (type == 3)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(at[8..], (ushort)value);
        }
        else
        {
            BinaryPrimitives.WriteUInt32LittleEndian(at[8..], value);
        }
    }

    /// <summary>One 1x1 frame of the given colour: a graphic control block, an image descriptor and three LZW codes (clear, the colour, end).</summary>
    private static byte[] Frame(byte colour) =>
    [
        0x21, 0xF9, 4, 0, 10, 0, 0, 0,
        0x2C, 0, 0, 0, 0, 1, 0, 1, 0, 0,
        2, 2, (byte)(0x44 | (colour << 3)), 0x01, 0
    ];
}
