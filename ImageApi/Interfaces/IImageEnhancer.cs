namespace ImageApi.Interfaces
{
    public interface IImageEnhancer
    {
        // In future, we’ll implement things like super-resolution here
        Task<byte[]> EnhanceAsync(Stream imageStream, string enhancementType);
    }
}
