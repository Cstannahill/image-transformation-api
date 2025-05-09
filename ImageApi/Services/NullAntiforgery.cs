using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
namespace ImageApi.Services
{
    public class NullAntiforgery : IAntiforgery
    {
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext context)
          => new AntiforgeryTokenSet(string.Empty, string.Empty, string.Empty, string.Empty);

        public AntiforgeryTokenSet GetTokens(HttpContext context)
          => new AntiforgeryTokenSet(string.Empty, string.Empty, string.Empty, string.Empty);

        public Task<bool> IsRequestValidAsync(HttpContext context)
          => Task.FromResult(true);

        public Task ValidateRequestAsync(HttpContext context)
          => Task.CompletedTask;
        public void SetCookieTokenAndHeader(HttpContext context)
        {
            // no-op
        }

    }
}