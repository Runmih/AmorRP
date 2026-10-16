using AmorRP.Server.Infrastructure.Security;
using Microsoft.AspNetCore.Http.Features;
using SkiaSharp;
namespace AmorRP.Server.Features.Media;

// Shared hostile-input boundary. Currency icons keep their strict 128px limit;
// group logos accept bounded larger sources and downscale before storage.
internal static class StaticImageUpload
{
    internal sealed record Image(byte[] Input, byte[] Png, int Width, int Height);
    private static bool AnimatedContainer(byte[] input, SKEncodedImageFormat format)
    {
        // Skia may decode only the fallback PNG frame; reject APNG control chunks explicitly.
        if (format == SKEncodedImageFormat.Png)
        {
            for (var offset = 8; offset + 12 <= input.Length;)
            {
                var size = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(input.AsSpan(offset, 4));
                if (input.AsSpan(offset + 4, 4).SequenceEqual("acTL"u8)) return true;
                if (size > input.Length - offset - 12) break;
                offset += (int)size + 12;
            }
        }
        if (format == SKEncodedImageFormat.Webp)
        {
            for (var offset = 12; offset + 8 <= input.Length;)
            {
                var size = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(input.AsSpan(offset + 4, 4));
                if (input.AsSpan(offset, 4).SequenceEqual("ANIM"u8) || input.AsSpan(offset, 4).SequenceEqual("ANMF"u8)) return true;
                if (size > input.Length - offset - 8) break;
                offset += (int)size + 8 + (int)(size % 2);
            }
        }
        return false;
    }
    public static async Task<Image> ReadAsync(HttpContext h, int byteLimit, int sourceDimension, int outputDimension, int outputBytes, CancellationToken ct)
    {
        var feature = h.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false }) feature.MaxRequestBodySize = byteLimit + 8192;
        ApiFault.Require(h.Request.HasFormContentType && (!h.Request.ContentLength.HasValue || h.Request.ContentLength <= byteLimit + 8192), 413, "image_too_large", "Image upload is too large or not multipart data.");
        IFormCollection form;
        try { form = await h.Request.ReadFormAsync(new FormOptions { MultipartBodyLengthLimit = byteLimit + 8192, MemoryBufferThreshold = 65536, ValueCountLimit = 1, MultipartHeadersLengthLimit = 4096 }, ct); }
        catch (InvalidDataException) { throw new ApiFault(413, "image_too_large", "Image upload exceeds its limits."); }
        ApiFault.Require(form.Files.Count == 1 && form.Files[0].Name == "file" && form.Count == 0, 422, "invalid_image", "Upload one image file.");
        var file = form.Files[0]; ApiFault.Require(file.Length > 0 && file.Length <= byteLimit, 413, "image_too_large", "Image exceeds the encoded-file limit.");
        using var memory = new MemoryStream(); await file.CopyToAsync(memory, ct); var input = memory.ToArray();
        using var data = SKData.CreateCopy(input); using var codec = SKCodec.Create(data);
        ApiFault.Require(codec != null && codec.EncodedFormat is SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Webp, 415, "unsupported_media_type", "Use a static PNG, JPG or WebP image.");
        ApiFault.Require(!AnimatedContainer(input, codec!.EncodedFormat), 422, "invalid_image", "Animated images are not supported.");
        var width = codec.Info.Width; var height = codec.Info.Height;
        ApiFault.Require(width > 0 && width <= sourceDimension && height > 0 && height <= sourceDimension && codec.FrameCount <= 1, 422, "invalid_image", $"Use a static image at most {sourceDimension} pixels on each side.");
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        ApiFault.Require(codec.GetPixels(bitmap.Info, bitmap.GetPixels()) == SKCodecResult.Success, 422, "invalid_image", "Image is malformed or incomplete.");
        var scale = Math.Min(1, (double)outputDimension / Math.Max(width, height));
        var outWidth = Math.Max(1, (int)Math.Round(width * scale)); var outHeight = Math.Max(1, (int)Math.Round(height * scale));
        using var resized = new SKBitmap(new SKImageInfo(outWidth, outHeight, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(resized))
        { canvas.Clear(SKColors.Transparent); canvas.DrawBitmap(bitmap, new SKRect(0, 0, outWidth, outHeight), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None)); }
        using var normalized = resized.Encode(SKEncodedImageFormat.Png, 100); var png = normalized.ToArray();
        ApiFault.Require(png.Length <= outputBytes, 422, "invalid_image", "Normalized image exceeds storage bounds.");
        return new(input, png, outWidth, outHeight);
    }
}
