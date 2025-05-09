using SkiaSharp;
using ImageApi.Interfaces;
using Svg.Skia;
using static System.Net.Mime.MediaTypeNames;

namespace ImageApi.Services
{
    public class SkiaImageProcessor : IImageProcessor
    {
        public async Task<byte[]> ResizeAsync(Stream imageStream, int width, int height, string outputFormat)
        {
            if (imageStream.CanSeek)
                imageStream.Position = 0;

            // Decode into an SKImage first
            using var managedStream = new SKManagedStream(imageStream);
            using var skImage = SKImage.FromEncodedData(managedStream)
                             ?? throw new ArgumentException("Invalid image file.", nameof(imageStream));

            // Get bitmap from the image
            using var inputBitmap = SKBitmap.FromImage(skImage);

            // Create the target info
            var targetInfo = new SKImageInfo(width, height);

            // Choose high-quality sampling (Lanczos3) for best results
            var sampling = new SKSamplingOptions(
              SKFilterMode.Linear,    // linear filtering
              SKMipmapMode.Linear     // linear mipmaps
            );

            // Perform the resize with the new API
            using var resizedBitmap = inputBitmap.Resize(targetInfo, sampling)
                                   ?? throw new InvalidOperationException("Resize failed.");

            using var resizedImage = SKImage.FromBitmap(resizedBitmap);

            // Pick encoder
            var fmt = outputFormat.ToLowerInvariant() switch
            {
                "jpg" or "jpeg" => SKEncodedImageFormat.Jpeg,
                "png" => SKEncodedImageFormat.Png,
                _ => SKEncodedImageFormat.Png
            };

            using var data = resizedImage.Encode(fmt, 90);
            return data.ToArray();
        }

        public async Task<byte[]> CropAsync(Stream imageStream, int x, int y, int width, int height, string outputFormat)
        {
            if (imageStream.CanSeek)
                imageStream.Position = 0;

            using var managedStream = new SKManagedStream(imageStream);
            using var skImage = SKImage.FromEncodedData(managedStream)
                             ?? throw new ArgumentException("Invalid image file.", nameof(imageStream));

            using var bitmap = SKBitmap.FromImage(skImage);

            // Ensure the crop rectangle is within bounds
            var cropRect = new SKRectI(
                Math.Clamp(x, 0, bitmap.Width - 1),
                Math.Clamp(y, 0, bitmap.Height - 1),
                Math.Clamp(x + width, 1, bitmap.Width),
                Math.Clamp(y + height, 1, bitmap.Height)
            );

            var subset = new SKBitmap();
            if (!bitmap.ExtractSubset(subset, cropRect))
                throw new ArgumentException("Crop rectangle is out of bounds.", nameof(imageStream));

            using var croppedImage = SKImage.FromBitmap(subset);

            // Pick encoder
            var fmt = outputFormat.ToLowerInvariant() switch
            {
                "jpg" or "jpeg" => SKEncodedImageFormat.Jpeg,
                "png" => SKEncodedImageFormat.Png,
                "webp" => SKEncodedImageFormat.Webp,
                "gif" => SKEncodedImageFormat.Gif,
                "bmp" => SKEncodedImageFormat.Bmp,
                "avif" => SKEncodedImageFormat.Avif,
                "heif" => SKEncodedImageFormat.Heif,
                "ico" => SKEncodedImageFormat.Ico,
                _ => throw new ArgumentException($"Unsupported format: {outputFormat}", nameof(outputFormat))
            };

            using var data = croppedImage.Encode(fmt, 90)
                            ?? throw new ArgumentException($"The {fmt} codec isn’t available.", nameof(outputFormat));

            return data.ToArray();
        }

        public async Task<byte[]> ConvertFormatAsync(Stream imageStream, string outputFormat)
        {
            // Reset stream
            if (imageStream.CanSeek)
                imageStream.Position = 0;

            // Decode the image
            using var managedStream = new SKManagedStream(imageStream);
            using var skImage = SKImage.FromEncodedData(managedStream)
                             ?? throw new ArgumentException("Invalid image file.", nameof(imageStream));

            // Pick encoder based on outputFormat
            var fmt = outputFormat.ToLowerInvariant() switch
            {
                "jpg" or "jpeg" => SKEncodedImageFormat.Jpeg,
                "png" => SKEncodedImageFormat.Png,
                "webp" => SKEncodedImageFormat.Webp,
                "gif" => SKEncodedImageFormat.Gif,
                "bmp" => SKEncodedImageFormat.Bmp,
                "avif" => SKEncodedImageFormat.Avif,
                "heif" => SKEncodedImageFormat.Heif,
                "ico" => SKEncodedImageFormat.Ico,

                _ => throw new ArgumentException($"Unsupported format: {outputFormat}", nameof(outputFormat))
            };

            // Encode with 90% quality
            using var data = skImage.Encode(fmt, 90)
              ?? throw new ArgumentException($"Encoding to {outputFormat} failed – codec not available.");

            return data.ToArray();
        }
        public async Task<byte[]> ApplyFilterAsync(Stream imageStream, string filterType, float? intensity, string outputFormat)
        {
            if (imageStream.CanSeek)
                imageStream.Position = 0;

            // Decode
            using var managed = new SKManagedStream(imageStream);
            using var skImage = SKImage.FromEncodedData(managed)
                             ?? throw new ArgumentException("Invalid image file.", nameof(imageStream));

            // Convert to bitmap
            using var bitmap = SKBitmap.FromImage(skImage);

            // Prepare surface
            var info = new SKImageInfo(bitmap.Width, bitmap.Height);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.Clear();

            // Paint with filter
            using var paint = new SKPaint();
            switch (filterType.ToLowerInvariant())
            {
                case "grayscale":
                    var matrix = new float[]
                    {
                0.2126f, 0.7152f, 0.0722f, 0, 0,
                0.2126f, 0.7152f, 0.0722f, 0, 0,
                0.2126f, 0.7152f, 0.0722f, 0, 0,
                0,       0,       0,       1, 0,
                    };
                    paint.ColorFilter = SKColorFilter.CreateColorMatrix(matrix);
                    break;

                case "invert":
                    var inv = new float[]
                    {
                -1,  0,  0, 0, 255,
                 0, -1,  0, 0, 255,
                 0,  0, -1, 0, 255,
                 0,  0,  0, 1,   0,
                    };
                    paint.ColorFilter = SKColorFilter.CreateColorMatrix(inv);
                    break;

                case "blur":
                    float sigma = intensity.GetValueOrDefault(5f);
                    paint.ImageFilter = SKImageFilter.CreateBlur(sigma, sigma);
                    break;

                default:
                    throw new ArgumentException($"Unsupported filter: {filterType}", nameof(filterType));
            }

            // Draw the original bitmap through the paint
            var rect = new SKRect(0, 0, bitmap.Width, bitmap.Height);
            canvas.DrawBitmap(bitmap, rect, paint);

            using var filteredImage = surface.Snapshot();
            using var pixmap = filteredImage.PeekPixels();

            // Pick encoder
            var fmt = outputFormat.ToLowerInvariant() switch
            {
                "jpg" or "jpeg" => SKEncodedImageFormat.Jpeg,
                "png" => SKEncodedImageFormat.Png,
                "webp" => SKEncodedImageFormat.Webp,
                _ => SKEncodedImageFormat.Png
            };

            using var data = filteredImage.Encode(fmt, 90)
                           ?? throw new ArgumentException($"Codec not available for {outputFormat}", nameof(outputFormat));

            return data.ToArray();
        }

        public async Task<byte[]> AddWatermarkAsync(
    Stream imageStream,
    string watermarkText,
    int fontSize,
    float opacity,
    int margin,
    string outputFormat)
        {
            if (imageStream.CanSeek)
                imageStream.Position = 0;

            // Decode
            using var managed = new SKManagedStream(imageStream);
            using var skImage = SKImage.FromEncodedData(managed)
                             ?? throw new ArgumentException("Invalid image file.", nameof(imageStream));

            // Prepare surface
            var info = new SKImageInfo(skImage.Width, skImage.Height);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.Clear();
            canvas.DrawImage(skImage, 0, 0);

            // Prepare paint for watermark text
            using var paint = new SKPaint
            {
                TextSize = fontSize,
                IsAntialias = true,
                Color = SKColors.White.WithAlpha((byte)(opacity * 255)),
                IsStroke = false
            };

            // Measure text size
            var textBounds = new SKRect();
            paint.MeasureText(watermarkText, ref textBounds);

            // Position in bottom-right
            float x = info.Width - textBounds.Width - margin;
            float y = info.Height - margin;

            canvas.DrawText(watermarkText, x, y, paint);

            using var watermarked = surface.Snapshot();

            // Pick encoder
            var fmt = outputFormat.ToLowerInvariant() switch
            {
                "jpg" or "jpeg" => SKEncodedImageFormat.Jpeg,
                "png" => SKEncodedImageFormat.Png,
                "webp" => SKEncodedImageFormat.Webp,
                _ => SKEncodedImageFormat.Png
            };

            using var data = watermarked.Encode(fmt, 90)
                           ?? throw new ArgumentException($"Codec not available for {outputFormat}", nameof(outputFormat));

            return data.ToArray();
        }


        public async Task<byte[]> CropRoundedAsync(
    Stream imageStream,
    int x, int y,
    int width, int height,
    int cornerRadius,
    string outputFormat)
        {
            if (imageStream.CanSeek)
                imageStream.Position = 0;

            // 1) Decode source
            using var managed = new SKManagedStream(imageStream);
            using var skImage = SKImage.FromEncodedData(managed)
                         ?? throw new ArgumentException("Invalid image file.");

            // 2) Create target surface with transparency
            var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            // 3) Define rounded rect
            var rr = new SKRoundRect(
              new SKRect(0, 0, width, height),
              cornerRadius,
              cornerRadius
            );

            // 4) Clip to rounded rect
            canvas.ClipRoundRect(rr, antialias: true);

            // 5) Draw the desired source region, offset so the crop region maps to (0,0)
            var srcRect = new SKRectI(x, y, x + width, y + height);
            var destRect = new SKRect(0, 0, width, height);
            canvas.DrawImage(skImage, srcRect, destRect);

            // 6) Snapshot and encode
            using var outputImage = surface.Snapshot();
            SKEncodedImageFormat fmt = outputFormat.ToLowerInvariant() switch
            {
                "jpg" or "jpeg" => SKEncodedImageFormat.Jpeg,
                "png" => SKEncodedImageFormat.Png,
                "webp" => SKEncodedImageFormat.Webp,
                _ => SKEncodedImageFormat.Png
            };
            using var data = outputImage.Encode(fmt, 90)
                           ?? throw new ArgumentException($"Codec not available for {outputFormat}.");
            return data.ToArray();
        }
    }
}
