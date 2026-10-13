using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UGTM.BuildingBlocks.Infrastructure.Middlewares;

public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected; there is no response left to write.
        }
        catch (AntiforgeryValidationException exception)
        {
            logger.LogWarning(
                "Rejected request with an invalid CSRF token. RequestId: {RequestId}",
                context.TraceIdentifier);
            await HandleExceptionAsync(context, exception);
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            logger.LogError(
                exception,
                "Unhandled request failure. RequestId: {RequestId}",
                context.TraceIdentifier);
            await HandleExceptionAsync(context, exception);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";

        var (statusCode, title) = exception switch
        {
            AntiforgeryValidationException =>
                (HttpStatusCode.BadRequest, "Invalid CSRF Token"),
            _ => (HttpStatusCode.InternalServerError, "Server Error")
        };

        context.Response.StatusCode = (int)statusCode;
        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = environment.IsDevelopment()
                ? exception.Message
                : "An error occurred while processing your request.",
            Instance = context.Request.Path
        };
        problemDetails.Extensions["requestId"] = context.TraceIdentifier;
        problemDetails.Extensions["timestamp"] = DateTime.UtcNow;

        return context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails));
    }
}
