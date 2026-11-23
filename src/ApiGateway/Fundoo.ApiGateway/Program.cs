//using System.Text.Json;
//using System.Threading.RateLimiting;
//using Microsoft.AspNetCore.RateLimiting;
//using Ocelot.DependencyInjection;
//using Ocelot.Middleware;

//var builder = WebApplication.CreateBuilder(args);

//// Load Ocelot configuration
//builder.Configuration.AddJsonFile(
//    "ocelot.json",
//    optional: false,
//    reloadOnChange: true);

//// Register Ocelot
//builder.Services.AddOcelot(builder.Configuration);

//// ASP.NET Core built-in rate limiting
//builder.Services.AddRateLimiter(options =>
//{
//    options.GlobalLimiter =
//        PartitionedRateLimiter.Create<HttpContext, string>(context =>
//        {
//            // 1. Get request path
//            string path =
//                context.Request.Path.Value?.ToLowerInvariant() ?? "";

//            // 2. Get client IP
//            string clientIp =
//                context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

//            // 3. Identify rate-limit group and its limit
//            string group;
//            int limit;

//            if (path == "/api/auth/login")
//            {
//                group = "login";
//                limit = 5;
//            }
//            else if (path == "/api/auth/register")
//            {
//                group = "register";
//                limit = 3;
//            }
//            else if (path == "/api/auth/forgot-password")
//            {
//                group = "forgot-password";
//                limit = 2;
//            }
//            else
//            {
//                group = "other";
//                limit = 120;
//            }

//            // 4. Create a unique partition for each IP and group
//            string partitionKey = $"{clientIp}:{group}";

//            // 5. Apply a separate fixed-window limiter to each partition
//            return RateLimitPartition.GetFixedWindowLimiter(
//                partitionKey: partitionKey,
//                factory: _ => new FixedWindowRateLimiterOptions
//                {
//                    PermitLimit = limit,
//                    Window = TimeSpan.FromMinutes(1),
//                    QueueLimit = 0,
//                    AutoReplenishment = true
//                });
//        });

//    // Response when the rate limit is exceeded
//    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

//    options.OnRejected = async (context, cancellationToken) =>
//    {
//        context.HttpContext.Response.ContentType = "application/json";

//        await context.HttpContext.Response.WriteAsync(
//            JsonSerializer.Serialize(new
//            {
//                message = "Too many requests. Please try again later."
//            }),
//            cancellationToken);
//    };
//});

//var app = builder.Build();

//// Health endpoint
//app.MapGet("/health", () =>
//{
//    return Results.Json(new
//    {
//        status = "Healthy",
//        service = "Fundoo.ApiGateway",
//        timestampUtc = DateTime.UtcNow
//    });
//});

//// Apply ASP.NET Core rate limiter
//app.UseRateLimiter();

//// Forward requests through Ocelot
//await app.UseOcelot();

//app.Run();

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using Ocelot.Configuration;
//using System.Threading.RateLimiting;
//using Microsoft.AspNetCore.RateLimiting;
using Ocelot.DependencyInjection;
using Ocelot.Logging;
using Ocelot.Middleware;
using Ocelot.QualityOfService.Polly;
using Polly;
using Polly.Registry;
using Polly.Retry;
using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
var builder = WebApplication.CreateBuilder(args);

// Load Ocelot configuration
builder.Configuration.AddJsonFile(
    "ocelot.json",
    optional: false,
    reloadOnChange: true);


//// ASP.NET Core built-in rate limiting
//builder.Services.AddRateLimiter(options =>
//{
//    options.GlobalLimiter =
//        PartitionedRateLimiter.Create<HttpContext, string>(context =>
//        {
//            // 1. Get request path
//            string path =
//                context.Request.Path.Value?.ToLowerInvariant() ?? "";

//            // 2. Get client IP
//            string clientIp =
//                context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

//            // 3. Identify rate-limit group and its limit
//            string group;
//            int limit;

//            if (path == "/api/auth/login")
//            {
//                group = "login";
//                limit = 5;
//            }
//            else if (path == "/api/auth/register")
//            {
//                group = "register";
//                limit = 3;
//            }
//            else if (path == "/api/auth/forgot-password")
//            {
//                group = "forgot-password";
//                limit = 2;
//            }
//            else
//            {
//                group = "other";
//                limit = 120;
//            }

//            // 4. Create a unique partition for each IP and group
//            string partitionKey = $"{clientIp}:{group}";

//            // 5. Apply a separate fixed-window limiter to each partition
//            return RateLimitPartition.GetFixedWindowLimiter(
//                partitionKey: partitionKey,
//                factory: _ => new FixedWindowRateLimiterOptions
//                {
//                    PermitLimit = limit,
//                    Window = TimeSpan.FromMinutes(1),
//                    QueueLimit = 0,
//                    AutoReplenishment = true
//                });
//        });

//    // Response when the rate limit is exceeded
//    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

//    options.OnRejected = async (context, cancellationToken) =>
//    {
//        context.HttpContext.Response.ContentType = "application/json";

//        await context.HttpContext.Response.WriteAsync(
//            JsonSerializer.Serialize(new
//            {
//                message = "Too many requests. Please try again later."
//            }),
//            cancellationToken);
//    };
//});
//builder.Services.AddHttpClient("FundooUserApi", client =>
//{
//    client.BaseAddress =
//        new Uri("https://localhost:7001/");

//    client.Timeout = TimeSpan.FromSeconds(10);
//});
builder.Services
    .AddOcelot(builder.Configuration)
    .AddPolly<FundooRetryPipelineProvider>();
builder.Services.AddHttpClient("FundooDownstream")
    .AddResilienceHandler("GatewayTimeout", pipeline =>
    {
        pipeline.AddTimeout(TimeSpan.FromSeconds(5));
    });
builder.Services.AddHealthChecks()  // add health checks
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy(),
        tags: new[] { "live" }
    );  // add health checks

var app = builder.Build();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/user/swagger.json", "Fundoo User API v1");
    options.SwaggerEndpoint("/swagger/notes/swagger.json", "Fundoo Notes API v1");
    options.SwaggerEndpoint("/swagger/notification/swagger.json", "Fundoo Notification API v1");
    options.RoutePrefix = "swagger";
});

// Health endpoint in asp .net core
//app.MapGet("/health", () =>
//{
//    return Results.Json(new
//    {
//        status = "Healthy",
//        service = "Fundoo.ApiGateway",
//        timestampUtc = DateTime.UtcNow
//    });
//});

// Health endpoint in ocelot
//app.Use(async (context, next) =>
//{
//    if (context.Request.Path.Equals(
//        "/health",
//        StringComparison.OrdinalIgnoreCase))
//    {
//        context.Response.StatusCode = StatusCodes.Status200OK;
//        context.Response.ContentType = "application/json";

//        await context.Response.WriteAsync(
//            JsonSerializer.Serialize(new
//            {
//                status = "Healthy",
//                service = "Fundoo.ApiGateway",
//                timestampUtc = DateTime.UtcNow
//            }));

//        return;
//    }

//    await next();
//});

// Apply ASP.NET Core rate limiter
//app.UseRateLimiter();

app.UseHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.UseHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// Forward requests through Ocelot
await app.UseOcelot();
ThreadPool.GetMinThreads(out int minWorkers, out int minIo);
ThreadPool.GetMaxThreads(out int maxWorkers, out int maxIo);

Console.WriteLine($"Processors: {Environment.ProcessorCount}");
Console.WriteLine($"Min workers: {minWorkers}, max workers: {maxWorkers}");
app.Run();

public sealed class FundooRetryPipelineProvider
    : PollyQoSResiliencePipelineProvider
{
    public FundooRetryPipelineProvider(
        IOcelotLoggerFactory loggerFactory,
        ResiliencePipelineRegistry<OcelotResiliencePipelineKey> registry)
        : base(loggerFactory, registry)
    {
    }

    protected override ResiliencePipelineBuilder<HttpResponseMessage>
     ConfigureCircuitBreaker(
         ResiliencePipelineBuilder<HttpResponseMessage> builder,
         DownstreamRoute route)
    {
        if (route.MetadataOptions.Metadata.TryGetValue(
            "EnableRetry", out var enabled)
            && enabled == "true")
        {
            builder.AddRateLimiter(
                new SlidingWindowRateLimiter(
                    new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 2,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 4,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            builder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,

                ShouldHandle = args =>
                    ValueTask.FromResult(
                        args.Outcome.Exception is HttpRequestException ||
                        (args.Outcome.Result is { } response &&
                         (response.StatusCode == HttpStatusCode.RequestTimeout ||
                          response.StatusCode == HttpStatusCode.TooManyRequests ||
                          (int)response.StatusCode >= 500))
                    )
            });
        }

        return base.ConfigureCircuitBreaker(builder, route);
    }
       
    
}
