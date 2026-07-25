# Global Error Handling Middleware — What, Why, and How

## The Problem in Your Code

Look at your `AuthController`. Every single endpoint has this **exact same pattern**:

```csharp
[HttpPost("login")]
public async Task<ActionResult<BaseResponse<AuthResponse>>> Login(LoginRequest request)
{
    try
    {
        // ← 1-2 lines of actual logic
        var response = await authService.LoginAsync(request);
        return Ok(BaseResponse<AuthResponse>.SuccessResponse("Login successful", response));
    }
    catch (ArgumentException e)         // ← repeated in EVERY endpoint
    {
        return BadRequest(BaseResponse<AuthResponse>.ErrorResponse(e.Message));
    }
    catch (Exception e)                 // ← repeated in EVERY endpoint
    {
        logger.LogError(e, "An error occurred while logging in");
        return StatusCode(500, BaseResponse<AuthResponse>.ErrorResponse("An error occurred..."));
    }
}
```

Count your try/catch blocks:
- **AuthController**: 8 endpoints × 3 catch blocks = **24 lines of repeated error handling**
- **StudentsController**: 8 endpoints × 2 catch blocks = **16 lines of repeated error handling**

That's **~40 lines** of copy-pasted code doing the same thing: *"if this exception type, return this status code."*

## What Is Middleware?

In ASP.NET Core, every HTTP request flows through a **pipeline of middleware** — a chain of components that each get a chance to process the request and response.

```
HTTP Request
    ↓
┌─────────────────────────┐
│  1. Exception Middleware │  ← catches ANY unhandled exception
├─────────────────────────┤
│  2. Authentication      │  ← validates JWT
├─────────────────────────┤
│  3. Authorization       │  ← checks [Authorize]
├─────────────────────────┤
│  4. Your Controller     │  ← runs your business logic
└─────────────────────────┘
    ↓
HTTP Response
```

The key insight: **middleware wraps everything below it**. If your controller throws an exception, it bubbles up through the pipeline. If you place error-handling middleware at the **top**, it catches everything — from any controller, any endpoint, without any try/catch blocks in your code.

## How It Works

The middleware is a class with one job: **call the next middleware, and if it throws, catch it and convert it to an HTTP response**.

```
Request comes in
    ↓
ExceptionMiddleware calls next()  ─── try
    ↓                                  │
  Controller runs                      │
    ↓                                  │
  Service throws ArgumentException     │
    ↓                                  │
  Exception bubbles up            ─── catch
    ↓
ExceptionMiddleware catches it
    ↓
Returns { success: false, message: "..." } with status 400
```

---

## The Implementation

### Step 1: Create the Middleware Class

```csharp
// Middleware/ExceptionMiddleware.cs
using System.Net;
using System.Text.Json;
using SchoolMaster.Application.DTOs;

namespace SchoolMaster.Api.Middleware;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);  // ← call the rest of the pipeline
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
            ArgumentException ex      => (HttpStatusCode.BadRequest, ex.Message),
            UnauthorizedAccessException ex => (HttpStatusCode.Unauthorized, ex.Message),
            KeyNotFoundException ex   => (HttpStatusCode.NotFound, ex.Message),
            _                         => (HttpStatusCode.InternalServerError,
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
```

### Step 2: Register it in Program.cs

```csharp
  var app = builder.Build();

  app.UseMiddleware<ExceptionMiddleware>();  // ← FIRST in the pipeline

  app.UseAuthentication();
  app.UseAuthorization();
```

---

## Summary of Benefits

1. **Cleaner Controllers**: Focus only on the success path.
2. **Centralized Logic**: One place to decide how every error in your system looks to the user.
3. **Consistent Responses**: Guaranteed that every error (even unexpected ones) follows your standard response format.
