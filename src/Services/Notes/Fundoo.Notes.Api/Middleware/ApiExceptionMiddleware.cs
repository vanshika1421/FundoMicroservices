using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fundoo.Notes.Api.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            logger.LogError(exception, "Unhandled request failure for {Path}", context.Request.Path);
            context.Response.Clear();
            context.Response.StatusCode = exception switch
            {
                DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
                ArgumentException => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status500InternalServerError
            };
            var problem = new ProblemDetails
            {
                Status = context.Response.StatusCode,
                Title = context.Response.StatusCode == 409 ? "Conflict" :
                    context.Response.StatusCode == 400 ? "Invalid request" : "Request failed",
                Detail = context.Response.StatusCode == 500 ? "An unexpected error occurred." : exception.Message,
                Instance = context.Request.Path
            };
            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
