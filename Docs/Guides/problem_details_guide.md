# Problem Details (RFC 7807): A Beginner's Guide

Right now, if a user queries a student that doesn't exist, your `ExceptionMiddleware.cs` catches the `KeyNotFoundException` and returns a custom JSON object using your `BaseResponse` wrapper:

```json
{
  "success": false,
  "message": "Student not found",
  "data": null
}
```

This is fine, but it creates a problem for the developers building mobile apps and websites that consume your API. If they switch to a different API, that new API might format errors entirely differently (e.g., `{"error": "Student not found", "code": 404 }`). Mobile developers hate having to write custom error-parsing logic for every single API they use.

**Problem Details for HTTP APIs (RFC 7807)** is an internet standard. It is a globally agreed-upon JSON format specifically designed for HTTP errors.

---

## 1. The Anatomy of Problem Details

Instead of inventing your own error format, RFC 7807 dictates you must return a JSON object with specific fields:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "The student with ID 5 was not found.",
  "instance": "/api/v1/students/5"
}
```

- **`type`**: A URL pointing to human-readable documentation about the error type.
- **`title`**: A short, recognizable summary of the problem (usually matching the HTTP Status code name).
- **`status`**: The HTTP status code (so clients don't have to check the HTTP headers to find it).
- **`detail`**: A specific, human-readable explanation of *exactly* what went wrong (e.g., "The student was not found").
- **`instance`**: The exact URL/route that caused the error.

### Extensions
You can also inject your own custom properties (extensions) into the JSON. For example, if FluentValidation fails, you could easily attach the specific validation failures!
```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Email": ["You must enter an email."],
    "Password": ["Password must be at least 6 characters."]
  }
}
```

---

## 2. How it's implemented in .NET (The Concept)

For years, developers had to install third-party libraries (like `Hellang.Middleware.ProblemDetails`) to do this. But as of .NET 7, Microsoft built Problem Details natively into the framework!

### Step 1: Configure `Program.cs`
You simply tell ASP.NET Core that you want to enable the Problem Details standard globally.

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. Enable Problem Details
builder.Services.AddProblemDetails();

var app = builder.Build();

// 2. Map standard exceptions (like validation failures) to Problem Details automatically
app.UseExceptionHandler();
```

### Step 2: Refactor `ExceptionMiddleware.cs`
Right now, your middleware manually serializes `BaseResponse<object>.FailureResponse`. You would change it to generate a `ProblemDetails` object instead.

Here is conceptually what the modification looks like:

```csharp
using Microsoft.AspNetCore.Mvc;

public class ExceptionMiddleware
{
    // ... setup and invoke ...

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        int statusCode = exception switch
        {
            ArgumentException => StatusCodes.Status400BadRequest,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status500InternalServerError
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = GetTitleForStatusCode(statusCode), // Helper to get "Bad Request", "Not Found", etc.
            Detail = exception.Message,
            Instance = context.Request.Path
        };

        // If it's a 500 error, you might want to hide the Exception stack trace from users!
        if (statusCode == 500)
        {
            problemDetails.Detail = "An unexpected error occurred.";
        }

        context.Response.ContentType = "application/problem+json"; // The official RFC MIME type!
        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsJsonAsync(problemDetails);
    }
}
```

---

## The Verdict

Switching to Problem Details is considered a **Best Practice** for modern Web APIs. 

While `BaseResponse` is great for successful requests where you want to wrap your data (e.g., returning the `Student` or `StaffDto`), using `ProblemDetails` for errors allows tools, browsers, and mobile apps to automatically understand and easily parse the exact cause of an error using recognized internet standards!
