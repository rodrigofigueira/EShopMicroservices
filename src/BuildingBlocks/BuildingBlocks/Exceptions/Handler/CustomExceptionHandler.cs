using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Exceptions.Handler;

public class CustomExceptionHandler
    (ILogger<CustomExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        SendErrorLog(logger, exception);
        var problemDetails = CreateObjectProblemDetails(httpContext, exception);
        EnrichObject(problemDetails, exception, httpContext);
        await WriteJson(problemDetails, httpContext, cancellationToken);
        return true;
    }

    private static void SendErrorLog(ILogger<CustomExceptionHandler> logger, Exception exception)
        => logger.LogError("Error Message: {exceptionMessage}, Time of occurrence {time}", exception.Message, DateTime.UtcNow);

    private static ProblemDetails CreateObjectProblemDetails(HttpContext httpContext, Exception exception)
    {
        return new()
        {
            Title = exception.Message,
            Detail = exception.GetType().Name,
            Status = ResolveStatusCode(httpContext, exception),
            Instance = httpContext.Request.Path
        };
    }

    private static int ResolveStatusCode(HttpContext httpContext, Exception exception) => exception switch
    {
        InternalServerException => httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError,
        ValidationException => httpContext.Response.StatusCode = StatusCodes.Status400BadRequest,
        BadRequestException => httpContext.Response.StatusCode = StatusCodes.Status400BadRequest,
        NotFoundException => httpContext.Response.StatusCode = StatusCodes.Status404NotFound,
        _ => httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError
    };

    private static void EnrichObject(ProblemDetails problemDetails, Exception exception, HttpContext httpContext)
    {
        AddFieldTraceId(problemDetails, httpContext);
        AddValidationExceptions(problemDetails, exception);
    }

    private static void AddFieldTraceId(ProblemDetails problemDetails, HttpContext httpContext)
        => problemDetails.Extensions.Add("traceId", httpContext.TraceIdentifier);

    private static void AddValidationExceptions(ProblemDetails problemDetails, Exception exception)
    {
        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions.Add("ValidationErrors", validationException.Errors);
        }
    }

    private static async Task WriteJson(ProblemDetails problemDetails, HttpContext httpContext, CancellationToken cancellationToken)
        => await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken: cancellationToken);
}
