using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolMaster.Application.DTOs;
using SchoolMaster.Domain.CustomException;
namespace SchoolMaster.Api.Middlewares;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Map exception types to HTTP status codes
        var (statusCode, message) = exception switch
        {
            AlreadyExistException ex => (HttpStatusCode.Conflict, ex.Message),
            InvalidOtpException ex => (HttpStatusCode.BadRequest, ex.Message),
            OtpExpiredException ex => (HttpStatusCode.BadRequest, ex.Message),
            UserNotFoundException ex => (HttpStatusCode.NotFound, ex.Message),
            ArgumentException ex => (HttpStatusCode.BadRequest, ex.Message),
            InvalidCredentialsException ex => (HttpStatusCode.Unauthorized, ex.Message),
            UnauthorizedAccessException ex => (HttpStatusCode.Unauthorized, ex.Message),
            EmailNotVerifiedException ex => (HttpStatusCode.Forbidden, ex.Message),
            AccountInactiveException ex => (HttpStatusCode.Forbidden, ex.Message),
            AccountLockedException ex => (HttpStatusCode.TooManyRequests, ex.Message),
            TenantMismatchException ex => (HttpStatusCode.Forbidden, ex.Message),
            KeyNotFoundException ex => (HttpStatusCode.NotFound, ex.Message),
            AcademicYearNotFoundException ex => (HttpStatusCode.NotFound, ex.Message),
            TermNotFoundException ex => (HttpStatusCode.NotFound, ex.Message),
            ClassNotFoundException ex => (HttpStatusCode.NotFound, ex.Message),
            SubjectNotFoundException ex => (HttpStatusCode.NotFound, ex.Message),
            PeriodNotFoundException ex => (HttpStatusCode.NotFound, ex.Message),
            DuplicateAcademicYearException ex => (HttpStatusCode.Conflict, ex.Message),
            DuplicateClassNameException ex => (HttpStatusCode.Conflict, ex.Message),
            DuplicateSubjectCodeException ex => (HttpStatusCode.Conflict, ex.Message),
            TermDateOutOfRangeException ex => (HttpStatusCode.UnprocessableEntity, ex.Message),
            TermDateOverlapException ex => (HttpStatusCode.UnprocessableEntity, ex.Message),
            PeriodTimeConflictException ex => (HttpStatusCode.Conflict, ex.Message),
            DuplicateAttendanceException ex => (HttpStatusCode.Conflict, ex.Message),
            StudentNotInClassException ex => (HttpStatusCode.UnprocessableEntity, ex.Message),
            // Catches concurrent write race conditions that bypass the in-memory upsert check.
            // PostgreSQL error code 23505 = unique_violation.
            DbUpdateException { InnerException: PostgresException { SqlState: "23505" } }
                => (HttpStatusCode.Conflict, "A duplicate record already exists."),
            _ => (HttpStatusCode.InternalServerError,
                                          "An unexpected error occurred. we are working to fix it.")
        };
        // Include the request line so a log entry points at the exact call that failed.
        var method = context.Request.Method;
        var path = context.Request.Path.Value;

        // Log the error (only log full details + stack trace for 500s; handled domain errors are expected).
        if (statusCode == HttpStatusCode.InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path}", method, path);
        }
        else
        {
            logger.LogWarning("Handled {ExceptionType} on {Method} {Path}: {Message}",
                exception.GetType().Name, method, path, exception.Message);
        }
        // If the response has already begun streaming, we cannot rewrite the status or body.
        // Bail rather than throw a secondary "response already started" exception that would
        // mask the real error.
        if (context.Response.HasStarted)
        {
            logger.LogError(exception, "Response already started; unable to write error response.");
            return;
        }

        // Write the response
        context.Response.Clear();
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";
        var response = new BaseResponse<object>(false, message, default);
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
    }
}