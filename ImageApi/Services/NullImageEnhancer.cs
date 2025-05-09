using ImageApi.Interfaces;

namespace ImageApi.Services
{
    public class NullImageEnhancer : IImageEnhancer
    {
        public Task<byte[]> EnhanceAsync(Stream imageStream, string enhancementType)
          => throw new NotImplementedException("AI enhancement will be implemented in a future version.");
    }
}
