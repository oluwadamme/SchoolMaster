using System.Net;
using System.Text.Json;
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
            UnauthorizedAccessException ex => (HttpStatusCode.Unauthorized, ex.Message),
            KeyNotFoundException ex => (HttpStatusCode.NotFound, ex.Message),
            _ => (HttpStatusCode.InternalServerError,
                                          "An unexpected error occurred")
        };
        // Log the error (only log full details for 500s)
        if (statusCode == HttpStatusCode.InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception occurred");
        }
        else
        {
            logger.LogWarning("Handled exception: {Message}", exception.Message);
        }
        // Write the response
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