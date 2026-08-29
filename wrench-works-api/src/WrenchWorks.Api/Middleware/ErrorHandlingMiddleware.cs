using System.Text.Json;
using FluentValidation;

namespace WrenchWorks.Api.Middleware;

public class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";
            var errors = ex.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage });
            await context.Response.WriteAsJsonAsync(new { code = "validation_error", errors });
        }
        catch (ConflictException ex)
        {
            context.Response.StatusCode = 409;
            await context.Response.WriteAsJsonAsync(new { code = "conflict", message = ex.Message, details = ex.Details });
        }
        catch (NotFoundException ex)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new { code = "not_found", message = ex.Message });
        }
        catch (ForbiddenException ex)
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { code = "forbidden", message = ex.Message });
        }
        catch (LimitReachedException ex)
        {
            context.Response.StatusCode = 422;
            await context.Response.WriteAsJsonAsync(new { code = "limit_reached", message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { code = "unauthorized", message = "Authentication required" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { code = "internal_error", message = "An unexpected error occurred" });
        }
    }
}

public class ConflictException(string message, object? details = null) : Exception(message)
{
    public object? Details { get; } = details;
}

public class NotFoundException(string message) : Exception(message);
public class ForbiddenException(string message) : Exception(message);
public class LimitReachedException(string message) : Exception(message);
