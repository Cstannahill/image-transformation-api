namespace ImageApi.Interfaces
{
    public interface IImageProcessor
    {
        Task<byte[]> ResizeAsync(Stream imageStream, int width, int height, string outputFormat);

        Task<byte[]> CropAsync(Stream imageStream, int x, int y, int width, int height, string outputFormat);

        // ...other methods: ConvertFormatAsync, ApplyFilterAsync, AddWatermarkAsync, etc.
        Task<byte[]> ConvertFormatAsync(Stream imageStream, string outputFormat);

        Task<byte[]> ApplyFilterAsync(Stream imageStream, string filterType, float? intensity, string outputFormat);

        Task<byte[]> AddWatermarkAsync(
    Stream imageStream,
    string watermarkText,
    int fontSize,
    float opacity,
    int margin,
    string outputFormat);

        Task<byte[]> CropRoundedAsync(
    Stream imageStream,
    int x, int y,
    int width, int height,
    int cornerRadius,
    string outputFormat);
    }
}