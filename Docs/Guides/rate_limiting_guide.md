# Rate Limiting: A Beginner's Guide

Imagine your `FirstApi` corresponds to a hugely popular application. What happens if a malicious hacker writes a script that rapidly attempts to guess a user's password?
```
Attempt 1: password123
Attempt 2: password1234
Attempt 3: admin123
... (5,000 attempts per second later)
```
If your server processes every single request, they will eventually guess the password. Even worse, handling 5,000 requests per second will eat up all your server's memory and CPU, causing it to crash and lock out legitimate users (this is called a **Denial of Service** or **DoS** attack).

Rate Limiting acts as your API's security bouncer. It stands at the door and says: *"You can only make 5 requests per minute. Anything more than that, and I will block you!"*

---

## The 4 Rate Limiting Algorithms

When you configure rate limiting, you have to choose *how* the bouncer counts requests. In modern .NET, there are 4 built-in algorithms you can use:

### 1. Fixed Window (The Simplest)
Time is divided into strict chunks (e.g., exactly 1 minute long: 12:00 to 12:01). You are allowed a maximum of 10 requests inside that window. If you make 10 requests at `12:00:01`, you are blocked for the remaining 59 seconds. Once the clock hits `12:01:00`, the counter resets to zero.

### 2. Sliding Window (The Smart Array)
Very similar to Fixed Window, but smoother. Instead of resetting strictly at the top of the minute, it looks backward at the *past* 60 seconds from the exact moment of your request. This prevents hackers from making 10 requests at `12:00:59`, and then immediately making another 10 requests at `12:01:00` (which would technically bypass a fixed window's immediate constraint).

### 3. Token Bucket (The Arcade Game)
Imagine you have a bucket that can hold 10 arcade tokens. Every request you make costs 1 token. 
If the bucket is empty, you are blocked. 
However, the bouncer slowly drops 1 new token into your bucket every 10 seconds. This allows users to occasionally "burst" their traffic (sending 10 requests rapidly), but forces them to slow down over time (1 request per 10 seconds afterward).

### 4. Concurrency Limit (The Restaurant Capacity)
This doesn't care about time at all. It only cares about *currently active* requests. 
*"I don't care how fast you send requests, but I will only process 100 requests AT THE EXACT SAME TIME."*
This is specifically used to prevent your server from running out of RAM or CPU under massive load.

---

## How Rate Limiting works in .NET (The Concept)

For years, developers had to use third-party libraries (like `AspNetCoreRateLimit`) for this. But since .NET 7, Microsoft built it directly into the framework using the `Microsoft.AspNetCore.RateLimiting` middleware.

Here is how you would conceptually implement Rate Limiting in your `FirstApi`.

### Step 1: Configure it in `Program.cs`
You decide what "Policies" you want to enforce. For instance, you might want a strict policy for Auth endpoints (to stop brute-force hacking), but a looser policy for GET endpoints.

```csharp
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

// 1. Create the bouncer logic
builder.Services.AddRateLimiter(options =>
{
    // If they get blocked, send back a 429 Too Many Requests
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Define a strict policy named "AuthLimit"
    options.AddFixedWindowLimiter("AuthLimit", config =>
    {
        // Only allow 5 requests per IP address...
        config.PermitLimit = 5;
        // ...every 1 minute.
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        // Don't queue extra requests, just block them instantly.
        config.QueueLimit = 0;
    });
});

var app = builder.Build();

// 2. Add the bouncer to the door
app.UseRateLimiter(); // This MUST go before UseAuthentication!
```

### Step 2: Apply the Policy to your Controllers
You just drop a tag over the endpoints you want to protect.

```csharp
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    // Force the user to obey the "AuthLimit" policy!
    [EnableRateLimiting("AuthLimit")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        // ... logic
    }
}
```

### The Result
If a user hits your `/api/Auth/login` endpoint 6 times in a single minute, the 6th request never reaches your Controller code. The `UseRateLimiter()` middleware automatically intercepts it and returns a `429 Too Many Requests` HTTP error code!

---

### In Summary

Rate Limiting is critical for production security. 
Without it, your login endpoints are open to infinite password brute-forcing, and your entire API is vulnerable to simple DoS attacks. By utilizing `.NET` natively built rate-limiting middleware, you can block bad actors before they even touch your business logic!
