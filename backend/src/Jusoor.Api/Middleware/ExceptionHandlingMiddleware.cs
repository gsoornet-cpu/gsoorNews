using System.Net;
using System.Text.Json;
using FluentValidation;

namespace Jusoor.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException validationException)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;

            var errors = validationException.Errors
                .Select(e => new { field = e.PropertyName, message = e.ErrorMessage });

            await context.Response.WriteAsync(JsonSerializer.Serialize(new { errors }));
        }
        catch (Exception exception)
        {
            // Full exception is logged server-side only. The client never
            // sees a stack trace or internal message — this is the
            // "never expose sensitive information through APIs simply
            // because it is technically available" principle applied to
            // error handling itself.
            _logger.LogError(exception, "Unhandled exception processing {Path}", context.Request.Path);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = "An unexpected error occurred." }));
        }
    }
}
