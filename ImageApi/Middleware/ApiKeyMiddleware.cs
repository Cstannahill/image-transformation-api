namespace ImageApi.Middleware
{
    public class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private const string HeaderName = "X-Api-Key";
        private readonly HashSet<string> _validKeys;

        public ApiKeyMiddleware(RequestDelegate next, IConfiguration config)
        {
            _next = next;
            _validKeys = config
                .GetSection("AllowedApiKeys")
                .Get<string[]>()
                .ToHashSet();
        }
        public async Task InvokeAsync(HttpContext ctx)
        {
            if (!ctx.Request.Headers.TryGetValue(HeaderName, out var providedKey)
                || !_validKeys.Contains(providedKey!))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsync("Missing or invalid API key.");
                return;
            }

            await _next(ctx);
        }
    }
}
