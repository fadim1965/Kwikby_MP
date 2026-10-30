using ImageMagick;

namespace WebDS_Test.Services.Images;

/// <summary>
/// Validates and normalises a product photo uploaded by a shop admin.
/// Rules that the shop must fix are rejected with a message; problems the server can fix
/// (transparency, metadata and image dimensions) are fixed here.
/// </summary>
public static class ProductImageProcessor
{
    /// <summary>Largest accepted upload, before processing.</summary>
    public const int MaxUploadBytes = 5 * 1024 * 1024;

    /// <summary>The shortest side must be at least this many pixels.</summary>
    public const int MinShortSide = 800;

    /// <summary>No side may exceed this many pixels.</summary>
    public const int MaxLongSide = 6000;

    /// <summary>Total pixel limit (width times height).</summary>
    public const long MaxPixels = 24_000_000;

    /// <summary>The stored photo is a square canvas of this size.</summary>
    public const int OutputSide = 1200;

    /// <summary>
    /// Validates and normalises an uploaded PNG.
    /// Throws ImageRejectedException if the shop must fix it.
    /// </summary>
    public static byte[] Process(Stream content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var bytes = ReadLimited(content, MaxUploadBytes);

        if (!IsPng(bytes))
        {
            throw new ImageRejectedException(
                "Product photos must be PNG files.");
        }

        try
        {
            using var image = new MagickImage(bytes);

            // Confirm the decoded image format.
            if (image.Format != MagickFormat.Png)
            {
                throw new ImageRejectedException(
                    "Product photos must be PNG files.");
            }

            var width = image.Width;
            var height = image.Height;

            // The shortest side must be at least 800 pixels.
            if (Math.Min(width, height) < MinShortSide)
            {
                throw new ImageRejectedException(
                    $"Product photos must be at least {MinShortSide} x {MinShortSide} pixels. " +
                    $"This photo is {width} x {height}.");
            }

            // Reject images that exceed either dimension or total-pixel limits.
            if (Math.Max(width, height) > MaxLongSide ||
                (long)width * height > MaxPixels)
            {
                throw new ImageRejectedException(
                    $"This photo is too large ({width} x {height}). " +
                    $"Please export it at {MaxLongSide} pixels or less on each side.");
            }

            // 1. Remove metadata such as EXIF, GPS and camera information.
            image.Strip();

            // 2. Flatten transparency onto white.
            // In Magick.NET 15.0.0, AlphaOption was renamed to AlphaAction.
            // AlphaAction.Remove removes the alpha channel by compositing
            // transparent areas against the image's BackgroundColor.
            image.BackgroundColor = MagickColors.White;
            image.Alpha(AlphaAction.Remove);

            // 3. Resize proportionally to fit within 1200 x 1200.
            // Greater = true means only shrink; never enlarge.
            var fit = new MagickGeometry(OutputSide, OutputSide)
            {
                IgnoreAspectRatio = false,
                Greater = true
            };

            image.Resize(fit);

            // 4. Extend to a 1200 x 1200 white square and center the image.
            image.BackgroundColor = MagickColors.White;
            image.Extent(OutputSide, OutputSide, Gravity.Center);

            // 5. Return the processed image as PNG.
            return image.ToByteArray(MagickFormat.Png);
        }
        catch (MagickException)
        {
            throw new ImageRejectedException(
                "That file is not a readable PNG image.");
        }
    }

    /// <summary>Checks the eight-byte PNG file signature.</summary>
    private static bool IsPng(byte[] bytes)
    {
        return bytes.Length >= 8 &&
               bytes[0] == 0x89 &&
               bytes[1] == 0x50 &&
               bytes[2] == 0x4E &&
               bytes[3] == 0x47 &&
               bytes[4] == 0x0D &&
               bytes[5] == 0x0A &&
               bytes[6] == 0x1A &&
               bytes[7] == 0x0A;
    }

    /// <summary>
    /// Reads the stream in chunks and rejects it as soon as it exceeds
    /// the configured maximum upload size.
    /// </summary>
    private static byte[] ReadLimited(Stream content, int limit)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];

        int bytesRead;
        while ((bytesRead = content.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + bytesRead > limit)
            {
                throw new ImageRejectedException(
                    $"Product photos must be {limit / (1024 * 1024)} MB or smaller.");
            }

            buffer.Write(chunk, 0, bytesRead);
        }

        if (buffer.Length == 0)
        {
            throw new ImageRejectedException("That file was empty.");
        }

        return buffer.ToArray();
    }
}
