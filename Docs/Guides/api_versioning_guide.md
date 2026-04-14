# API Versioning: A Beginner's Guide

Imagine your `SchoolMaster` becomes a massive success. Thousands of mobile apps and websites are using your endpoints to fetch students. 

One day, you decide to restructure the data format of a Student. Instead of `'fullName': 'John Doe'`, you want to split it into `'name': { 'firstName': 'John', 'lastName': 'Doe' }`. 

If you just change the code and deploy it, **every app using your API will instantly crash** because it expects a single string for 'fullName', not an object. 

This is what we call a **"Breaking Change."**

API Versioning is the solution. It allows you to build a completely new Version 2 (v2) of your API with the new data structure, while keeping Version 1 (v1) running exactly as it was, so older apps don't break. 

---

## The 4 Ways to Version an API

When a mobile app sends a GET request to your API, how does it tell you *which* version it wants? There are 4 industry-standard ways to specify the version:

### 1. URL Path Versioning (Most Common)
You put the version number directly inside the URL. 
- **Pros:** It's incredibly obvious just by looking at the URL. You can easily test it in a browser.
- **Cons:** Technically violates REST purist rules (because the URL should represent the *resource*, like a "Student", not the "Version of a Student").
```http
GET https://api.yourdomain.com/api/v1/students
GET https://api.yourdomain.com/api/v2/students
```

### 2. Query String Versioning (The Default in .NET)
You append the version parameter to the end of the URL.
- **Pros:** Extremely easy to configure. Doesn't break existing routes.
- **Cons:** Can make URLs messy.
```http
GET https://api.yourdomain.com/api/students?api-version=1.0
GET https://api.yourdomain.com/api/students?api-version=2.0
```

### 3. Header Versioning
The URL stays completely clean, but the client must send a custom HTTP header indicating the version.
- **Pros:** Very clean URLs. Satisfies REST purists.
- **Cons:** Harder to test. You can't just copy/paste a URL to a friend, because the version is hidden in the request headers.
```http
GET https://api.yourdomain.com/api/students
X-Api-Version: 2.0
```

### 4. MediaType / Accept Header Versioning (Advanced)
Similar to headers, but it modifies the standard `Accept` header. Usually used at massive scale (like GitHub's API).
```http
GET https://api.yourdomain.com/api/students
Accept: application/vnd.schoolmaster.v2+json
```

---

## How It's Done in .NET (The Concept)

Adding versioning to ASP.NET Core involves roughly 3 steps. *(You don't need to do this now, this is just how it conceptually works!)*

### Step 1: Install the Microsoft Packages
You install `Asp.Versioning.Mvc`. This library does all the heavy lifting.

### Step 2: Configure `Program.cs`
You tell your application which versioning strategy you want to use.
```csharp
// Example conceptually:
builder.Services.AddApiVersioning(options =>
{
    // If a request has no version, assume v1.0
    options.DefaultApiVersion = new ApiVersion(1, 0); 
    options.AssumeDefaultVersionWhenUnspecified = true;

    // Report the api versions your API supports in the response headers
    options.ReportApiVersions = true; 

    // Tell the API how to look for versions (e.g., Query String AND Header)
    options.ApiVersionReader = ApiVersionReader.Combine(
        new QueryStringApiVersionReader("api-version"),
        new HeaderApiVersionReader("X-Api-Version"));
});
```

### Step 3: Decorate your Controllers
You tag your controllers to tell the router which version they belong to. You can even have the same controller support multiple versions, and swap out individual HTTP verbs!

**Example: The Old Controller (v1)**
```csharp
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0", Deprecated = true)] // Tag it as deprecated so developers know to upgrade!
public class StudentsController : ControllerBase
{
    // This returns { fullName: "John Doe" }
    [HttpGet]
    public IActionResult GetV1Students() { ... }
}
```

**Example: The New Controller (v2)**
```csharp
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("2.0")] 
public class StudentsV2Controller : ControllerBase
{
    // This returns { name: { first: "John", last: "Doe" } }
    [HttpGet]
    public IActionResult GetV2Students() { ... }
}
```

---

### In Summary

API Versioning is a contract between you and the people consuming your data. Unless you are the ONLY person building the UI/Apps for the API (where you can safely coordinate everything at exactly the same time), you must use versioning so that you can freely scale and radically improve your backend logic without accidentally breaking the systems that depend on it!
