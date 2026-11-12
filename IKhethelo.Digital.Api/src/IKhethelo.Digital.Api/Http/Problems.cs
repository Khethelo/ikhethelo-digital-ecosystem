using IKhethelo.Digital.Application;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IKhethelo.Digital.Api.Http;

public static class Problems
{
    public static ProblemDetails Create(HttpContext context, int status, string? title = null, Dictionary<string, string[]>? errors = null)
    {
        ProblemDetails problem = errors is null ? new() : new ValidationProblemDetails(errors);
        problem.Status = status; problem.Type = "about:blank";
        problem.Title = title ?? (status switch { 400 => "Bad request.", 401 => "Authentication is required.", 403 => "Access is forbidden.",
            404 => "The requested resource was not found.", 413 => "Request body is too large.", 415 => "Use application/json.",
            429 => "Too many requests.", _ => "The request could not be completed." });
        problem.Extensions["traceId"] = context.TraceIdentifier;
        return problem;
    }
    public static Task WriteAsync(HttpContext context, int status, string? title = null, Dictionary<string, string[]>? errors = null)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync((object)Create(context, status, title, errors), options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}

public sealed class ErrorMiddleware(RequestDelegate next, ILogger<ErrorMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path == "/api/v1/auth/login") context.Response.Headers.CacheControl = "no-store";
        if (context.Request.Path.StartsWithSegments("/api/v1/admin-users")) context.Response.Headers.CacheControl = "no-store";
        context.Response.OnCompleted(() =>
        {
            var route = (context.GetEndpoint() as Microsoft.AspNetCore.Routing.RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
            logger.LogInformation("HTTP {Method} {Route} completed {StatusCode}; tenant {TenantCode}; trace {TraceId}",
                context.Request.Method, route, context.Response.StatusCode, context.Items["ResolvedTenantCode"], context.TraceIdentifier);
            return Task.CompletedTask;
        });
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { context.Abort(); }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            var failure = exception as RequestFailure;
            var pg = exception as PostgresException ?? (exception as DbUpdateException)?.InnerException as PostgresException;
            var status = failure?.Status ?? (exception is BadHttpRequestException bad ? bad.StatusCode :
                pg?.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected ? 409 : 500);
            if (status == 500) logger.LogError("Request failed. TraceId {TraceId}; exception type {ExceptionType}; inner type {InnerType}; database state {DatabaseState}",
                context.TraceIdentifier, exception.GetType().Name, exception.InnerException?.GetType().Name, pg?.SqlState);
            // Do not log raw exception objects: provider details may contain SQL or applicant PII.
            await Problems.WriteAsync(context, status, failure?.Message, failure?.Errors);
        }
    }
}
