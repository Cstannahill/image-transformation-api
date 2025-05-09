using ImageApi.Helpers;
using ImageApi.Interfaces;
using ImageApi.Middleware;
using ImageApi.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Prometheus;
using Serilog;
using System.Diagnostics;
using System.Threading.RateLimiting;

Counter TotalRequests = Metrics
    .CreateCounter("imageapi_requests_total", "Total requests", new CounterConfiguration
    {
        LabelNames = new[] { "method", "path", "apikey" }
    });
var builder = WebApplication.CreateBuilder(args);
// BUILDER CONFIG

builder.Configuration
  .SetBasePath(Directory.GetCurrentDirectory())
  .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
  .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
  .AddEnvironmentVariables();
ApiKeyStore.Initialize(builder.Configuration);
// LOGGER CONFIG
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();
builder.Host.UseSerilog();

// 1) Register our services
builder.Services.AddSingleton<IImageProcessor, SkiaImageProcessor>();
builder.Services.AddSingleton<IImageEnhancer, NullImageEnhancer>(); // stub that throws/not implemented
builder.Services.AddSingleton<IAntiforgery, NullAntiforgery>();
builder.Services.AddSingleton<CollectorRegistry>(_ => Metrics.DefaultRegistry);
builder.Services.AddSingleton<MetricServer>(_ => new MetricServer(port: 9091));
// 2) Add minimal API explorer + Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddRateLimiter(options =>
{
    
    options.OnRejected = (context, ct) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers["Retry-After"] = retryAfter.TotalSeconds.ToString();
        }
        return new ValueTask();
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var key = httpContext.Request.Headers["X-Api-Key"].FirstOrDefault() ?? "";
        var limits = ApiKeyStore.GetLimitsForKey(key);

        return RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: key,
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = limits.RequestsPerMinute,
                TokensPerPeriod = limits.RequestsPerMinute,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                AutoReplenishment = true,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// BUILDER BUILD
var app = builder.Build();
// START METRICS SERVER
var metricServer = app.Services.GetRequiredService<MetricServer>();
metricServer.Start();
// 3) USE STATEMENTS
app.UseAntiforgery();
//Enable Swagger in Dev
app.UseSwagger();
app.UseSwaggerUI();
// FOR WWWROOT Index.html for testing
app.UseDefaultFiles();   // will look for index.html
app.UseStaticFiles();
// METRICS (1. coolects request, duration, counts, etc. 2. exposes /metrics on same port)

// Rate Limiting / Monetization
app.UseMiddleware<ApiKeyMiddleware>();
app.UseRateLimiter();
//  daily-rate limit middleware
app.Use(async (ctx, next) =>
{
    var key = ctx.Request.Headers["X-Api-Key"].FirstOrDefault() ?? "";
    var limits = ApiKeyStore.GetLimitsForKey(key);
    var todayCount = DailyQuotaStore.Increment(key);

    if (todayCount > limits.DailyLimit)
    {
        ctx.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await ctx.Response.WriteAsync("Daily quota exceeded.");
        return;
    }

    // optionally, add headers so clients can see remaining quota
    ctx.Response.Headers["X-Daily-Quota-Used"] = todayCount.ToString();
    ctx.Response.Headers["X-Daily-Quota-Limit"] = limits.DailyLimit.ToString();

    await next();
});
// REQUEST LOGGING
app.Use(async (ctx, next) =>
{
    var sw = Stopwatch.StartNew();
    var apiKey = ctx.Request.Headers["X-Api-Key"].FirstOrDefault() ?? "none";
    var path = ctx.Request.Path;
    Log.Information("Incoming request {Method} {Path} from API Key {ApiKey}",
                    ctx.Request.Method, path, apiKey);

    await next();

    sw.Stop();
    Log.Information("Completed {Method} {Path} for API Key {ApiKey} => {StatusCode} in {Elapsed:0.000}ms",
                    ctx.Request.Method, path, apiKey, ctx.Response.StatusCode, sw.Elapsed.TotalMilliseconds);
});
//Increment Counters in Middleware
app.Use(async (ctx, next) => {
    var apiKey = ctx.Request.Headers["X-Api-Key"].FirstOrDefault() ?? "none";
    TotalRequests.WithLabels(ctx.Request.Method, ctx.Request.Path, apiKey).Inc();
    await next();
});

// 4) Map endpoints
// RESIZE
app.MapPost("/resize",
    async (
      IImageProcessor proc,
      [FromForm] IFormFile file,
      [FromForm] int width,
      [FromForm] int height,
      [FromForm] string fmt
    ) =>
    {
        if (file is null || file.Length == 0)
            return Results.BadRequest("File missing.");

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var output = await proc.ResizeAsync(ms, width, height, fmt);
        var contentType = fmt.Equals("ico", StringComparison.OrdinalIgnoreCase)
            ? "image/x-icon"
            : $"image/{fmt.ToLowerInvariant()}";

        return Results.File(output, contentType);

      
    })
.Accepts<IFormFile>("multipart/form-data")
.WithName("ResizeImage")
.WithOpenApi();
// CROP
app.MapPost("/crop",
    async (
        IImageProcessor proc,
        [FromForm] IFormFile file,
        [FromForm] int x,
        [FromForm] int y,
        [FromForm] int width,
        [FromForm] int height,
        [FromForm] string fmt
    ) =>
    {
        if (file is null || file.Length == 0)
            return Results.BadRequest("No file provided.");

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        byte[] output;
        try
        {
            output = await proc.CropAsync(ms, x, y, width, height, fmt);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }

        var contentType = fmt.Equals("ico", StringComparison.OrdinalIgnoreCase)
             ? "image/x-icon"
             : $"image/{fmt.ToLowerInvariant()}";

        return Results.File(output, contentType);
    })
.Accepts<IFormFile>("multipart/form-data")
.Produces<byte[]>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.WithName("CropImage")
.WithOpenApi();
//CROP ROUNDED
app.MapPost("/crop/rounded",
    async (
      IImageProcessor proc,
      [FromForm] IFormFile file,
      [FromForm] int x,
      [FromForm] int y,
      [FromForm] int width,
      [FromForm] int height,
      [FromForm] int radius,
      [FromForm] string fmt
    ) =>
    {
        if (file == null || file.Length == 0)
            return Results.BadRequest("No file provided.");

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        byte[] output;
        try
        {
            output = await proc.CropRoundedAsync(ms, x, y, width, height, radius, fmt);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }

        var ct = fmt.Equals("ico", StringComparison.OrdinalIgnoreCase)
            ? "image/x-icon"
            : $"image/{fmt.ToLowerInvariant()}";
        return Results.File(output, ct);
    })
.Accepts<IFormFile>("multipart/form-data")
.Produces<byte[]>(200)
.Produces(400)
.WithName("CropRounded")
.WithOpenApi();
// CONVERT
app.MapPost("/convert",
    async (
        IImageProcessor proc,
        [FromForm] IFormFile file,
        [FromForm] string fmt
    ) =>
    {
        if (file is null || file.Length == 0)
            return Results.BadRequest("No file provided.");

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        byte[] output;
        try
        {
            output = await proc.ConvertFormatAsync(ms, fmt);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }

        var contentType = fmt.Equals("ico", StringComparison.OrdinalIgnoreCase)
            ? "image/x-icon"
            : $"image/{fmt.ToLowerInvariant()}";

        return Results.File(output, contentType);
    })
.Accepts<IFormFile>("multipart/form-data")
.Produces<byte[]>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.WithName("ConvertFormat")
.WithOpenApi();
// ADD FILTERS
app.MapPost("/filter",
    async (
        IImageProcessor proc,
        [FromForm] IFormFile file,
        [FromForm] string type,
        [FromForm] float? intensity,
        [FromForm] string fmt
    ) =>
    {
        if (file == null || file.Length == 0)
            return Results.BadRequest("No file provided.");

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        byte[] output;
        try
        {
            output = await proc.ApplyFilterAsync(ms, type, intensity, fmt);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }

        return Results.File(output, $"image/{fmt.ToLower()}");
    })
.Accepts<IFormFile>("multipart/form-data")
.Produces<byte[]>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.WithName("ApplyFilter")
.WithOpenApi();
// ADD WATERMARK
app.MapPost("/watermark",
    async (
        IImageProcessor proc,
        [FromForm] IFormFile file,
        [FromForm] string text,
        [FromForm] int fontSize,
        [FromForm] float opacity,
        [FromForm] int margin,
        [FromForm] string fmt
    ) =>
    {
        if (file == null || file.Length == 0)
            return Results.BadRequest("No file provided.");

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        byte[] output;
        try
        {
            output = await proc.AddWatermarkAsync(ms, text, fontSize, opacity, margin, fmt);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }

        // image/x-icon for ico, else image/{fmt}
        var contentType = fmt.Equals("ico", StringComparison.OrdinalIgnoreCase)
            ? "image/x-icon"
            : $"image/{fmt.ToLowerInvariant()}";

        return Results.File(output, contentType);
    })
.Accepts<IFormFile>("multipart/form-data")
.Produces<byte[]>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.WithName("AddWatermark")
.WithOpenApi();
// HEALTH PROBE ROUTE
app.MapGet("/health", () => Results.Ok("Healthy")).ExcludeFromDescription();


// RUN
app.Run();
