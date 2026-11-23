using Fundoo.User.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Fundoo.User.Api.Authentication.Google;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text;
using FluentValidation;
using Fundoo.User.Api.Validation;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers(options =>
{
    options.Filters.Add<FluentValidationActionFilter>();
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
});
builder.Services.AddScoped<FluentValidationActionFilter>();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
// Health check registration
builder.Services
    .AddHealthChecks()
    .AddCheck(
        "user-api",
        () => HealthCheckResult.Healthy(
            "Fundoo User API is running."),
        tags: new[] { "live" });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

//Swagger implementation
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT access token."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services
    .AddHttpClient("NotificationService", client =>
    {
        var baseUrl = builder.Configuration["NotificationService:BaseUrl"]
            ?? throw new InvalidOperationException(
                "Notification Service BaseUrl is not configured.");

        client.BaseAddress = new Uri(baseUrl);

        // Timeout is configured by the resilience handler below.
        client.Timeout = Timeout.InfiniteTimeSpan;
    })
   .AddStandardResilienceHandler(options =>
   {
       // Total request timeout
       options.TotalRequestTimeout.Timeout =
           TimeSpan.FromSeconds(30);

       // Timeout for one attempt
       options.AttemptTimeout.Timeout =
           TimeSpan.FromSeconds(25);

       // Retry configuration
       options.Retry.MaxRetryAttempts = 3;

       options.Retry.DisableFor(
           HttpMethod.Post,
           HttpMethod.Patch,
           HttpMethod.Options,
           HttpMethod.Head,
           HttpMethod.Connect,
           HttpMethod.Trace);

       // Circuit Breaker configuration
       options.CircuitBreaker.FailureRatio = 0.5;

       options.CircuitBreaker.MinimumThroughput = 10;

       options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);

       
       options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
       // Concurrency Limiter (Bulkhead)
       options.RateLimiter.DefaultRateLimiterOptions.PermitLimit = 5;

       options.RateLimiter.DefaultRateLimiterOptions.QueueLimit = 5;
   });

builder.Services.AddUserInfrastructure(builder.Configuration);

var signingKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException(
        "JWT signing key is not configured.");

var issuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException(
        "JWT issuer is not configured.");

var audience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException(
        "JWT audience is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,

            ValidateAudience = true,
            ValidAudience = audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(signingKey)),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    })
    .AddFundooGoogleAuthentication(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    //ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
   // ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.Run();
