using System.Diagnostics;
using Abhyanvaya.API.ProblemDetails;
using Abhyanvaya.Application.Exceptions;
using Abhyanvaya.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

namespace Abhyanvaya.API.ExceptionHandling;

/// <summary>
/// Centralized exception handler producing RFC7807 ProblemDetails for all API errors.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, problem) = MapException(httpContext, exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception. TraceId={TraceId}", problem.Extensions[ApiProblemExtensions.TraceId]);
        }
        else if (exception is ConcurrencyConflictException or DbUpdateConcurrencyException)
        {
            _logger.LogWarning(
                "Concurrency conflict. TraceId={TraceId} Path={Path}",
                problem.Extensions[ApiProblemExtensions.TraceId],
                httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private (int StatusCode, MvcProblemDetails Problem) MapException(HttpContext httpContext, Exception exception)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        return exception switch
        {
            ConcurrencyConflictException concurrency => (
                StatusCodes.Status409Conflict,
                CreateProblem(
                    ApiProblemTypes.ConcurrencyConflict,
                    "Concurrency conflict",
                    StatusCodes.Status409Conflict,
                    concurrency.Message,
                    traceId,
                    ApiErrorCodes.ConcurrencyConflict,
                    reloadRequired: concurrency.ReloadRequired)),

            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                CreateProblem(
                    ApiProblemTypes.ConcurrencyConflict,
                    "Concurrency conflict",
                    StatusCodes.Status409Conflict,
                    ConcurrencyConflictException.ForAttendanceModule().Message,
                    traceId,
                    ApiErrorCodes.ConcurrencyConflict,
                    reloadRequired: true)),

            ValidationException validation => (
                StatusCodes.Status400BadRequest,
                CreateValidationProblem(validation, traceId)),

            NotFoundException notFound => (
                StatusCodes.Status404NotFound,
                CreateProblem(
                    ApiProblemTypes.NotFound,
                    "Resource not found",
                    StatusCodes.Status404NotFound,
                    notFound.Message,
                    traceId,
                    ApiErrorCodes.NotFound)),

            KeyNotFoundException keyNotFound => (
                StatusCodes.Status404NotFound,
                CreateProblem(
                    ApiProblemTypes.NotFound,
                    "Resource not found",
                    StatusCodes.Status404NotFound,
                    keyNotFound.Message,
                    traceId,
                    ApiErrorCodes.NotFound)),

            UnauthorizedAccessException => (
                StatusCodes.Status403Forbidden,
                CreateProblem(
                    ApiProblemTypes.Unauthorized,
                    "Access denied",
                    StatusCodes.Status403Forbidden,
                    "Access denied for this resource.",
                    traceId,
                    ApiErrorCodes.Unauthorized)),

            DomainException domain => (
                StatusCodes.Status400BadRequest,
                CreateProblem(
                    ApiProblemTypes.DomainRule,
                    "Domain rule violation",
                    StatusCodes.Status400BadRequest,
                    domain.Message,
                    traceId,
                    ApiErrorCodes.DomainRuleViolation)),

            InvalidOperationException invalid => (
                StatusCodes.Status400BadRequest,
                CreateProblem(
                    ApiProblemTypes.DomainRule,
                    "Invalid operation",
                    StatusCodes.Status400BadRequest,
                    invalid.Message,
                    traceId,
                    ApiErrorCodes.DomainRuleViolation)),

            DbUpdateException dbUpdate => (
                StatusCodes.Status400BadRequest,
                CreateProblem(
                    ApiProblemTypes.Database,
                    "Database error",
                    StatusCodes.Status400BadRequest,
                    _environment.IsDevelopment() ? dbUpdate.Message : "A database error occurred.",
                    traceId,
                    ApiErrorCodes.DatabaseError)),

            _ => (
                StatusCodes.Status500InternalServerError,
                CreateProblem(
                    ApiProblemTypes.Internal,
                    "Internal server error",
                    StatusCodes.Status500InternalServerError,
                    _environment.IsDevelopment() ? exception.Message : "An unexpected error occurred.",
                    traceId,
                    ApiErrorCodes.InternalError))
        };
    }

    private static MvcProblemDetails CreateProblem(
        string type,
        string title,
        int status,
        string detail,
        string traceId,
        string errorCode,
        bool reloadRequired = false)
    {
        var problem = new MvcProblemDetails
        {
            Type = type,
            Title = title,
            Status = status,
            Detail = detail
        };

        problem.Extensions[ApiProblemExtensions.ErrorCode] = errorCode;
        problem.Extensions[ApiProblemExtensions.TraceId] = traceId;
        problem.Extensions[ApiProblemExtensions.ReloadRequired] = reloadRequired;
        return problem;
    }

    private static MvcProblemDetails CreateValidationProblem(ValidationException exception, string traceId)
    {
        var problem = CreateProblem(
            ApiProblemTypes.Validation,
            "Validation failed",
            StatusCodes.Status400BadRequest,
            exception.Message,
            traceId,
            ApiErrorCodes.ValidationFailed);

        problem.Extensions[ApiProblemExtensions.ValidationErrors] = exception.Errors;
        return problem;
    }
}
