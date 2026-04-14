# Clean Architecture in .NET: A Deep Dive

Right now, your `SchoolMaster` project uses a classic **N-Tier Architecture** (specifically a 3-tier architecture). You have separated your logic neatly into folders:
`Controllers (Presentation) → Services (Business Logic) → Repositories (Data Access) → Database`

This is a fantastic architecture for small-to-medium apps! But there is one major flaw as your application scales into an enterprise-grade system: **Everything is compiled into a single project file (`SchoolMaster.csproj`).** 

This means a junior developer could accidentally inject your `SchoolMasterContext` (your database) directly into your `AuthController`, completely bypassing your `AuthService` and ruining the architecture. There is nothing *physically* stopping them.

### Enter Clean Architecture (The Onion Architecture)

Clean Architecture solves this by splitting your single project into **four completely separate physical class libraries (`.csproj` files)**. 

The golden rule of Clean Architecture is **The Dependency Rule**: *Dependencies must always point inward toward the core.* 

Here is how your `SchoolMaster` would be physically split up:

---

## The 4 Layers of Clean Architecture

### 1. The Core: `SchoolMaster.Domain`
This is the center of the onion. It contains your business entities and nothing else.
**Rule:** It cannot reference ANY other project. It cannot have any NuGet packages installed (no Entity Framework, no MailKit). It is pure, basic C# code.

**What goes here:**
*   `Models/Student.cs`
*   `Models/Staff.cs`
*   `Models/User.cs`
*   Custom Exceptions (e.g., `DomainException`)
*   Enums

### 2. The Use Cases: `SchoolMaster.Application`
This layer defines *what* your application actually does (the business logic). 
**Rule:** It can only reference the `Domain` project. It still has no idea what a database is, or what the internet is.

**What goes here:**
*   `Services/StudentService.cs`
*   `Services/AuthService.cs`
*   `DTOs/` (e.g., `RegisterRequest`, `StudentDto`)
*   **The Interfaces!** (`IStudentRepository`, `IEmailService`, `IAuthService`). 
    *   *Note: Building interfaces here is crucial because the Application layer dictates the "contract" that the outer layers must fulfill!*

### 3. The Outside World: `SchoolMaster.Infrastructure`
This layer is responsible for talking to external systems: The database, the file system, or third-party APIs (like Gmail).
**Rule:** It references `Application` and `Domain`. This is the ONLY project where you install heavy NuGet packages like `Microsoft.EntityFrameworkCore.PostgreSQL` or `MailKit`.

**What goes here:**
*   `Data/SchoolMasterContext.cs` (Entity Framework setup)
*   `Repositories/StudentRepository.cs` (Implements `IStudentRepository` from the Application layer)
*   `Services/EmailService.cs` (Implements `IEmailService` from the Application layer using MailKit)

### 4. The Entry Point: `SchoolMaster.Api`
This is the app that actually boots up and receives HTTP requests over the internet.
**Rule:** It references `Application` and `Infrastructure` (but only uses Infrastructure so it can inject the dependencies in `Program.cs`).

**What goes here:**
*   `Controllers/StudentsController.cs`
*   `Program.cs` (Where you assign `builder.Services.AddScoped<IStudentRepository, StudentRepository>()`)
*   `appsettings.json`
*   Middlewares (e.g., `ExceptionMiddleware`)

---

## The Big "Aha!" Moment

You might be wondering: *"If the Application layer holds `StudentService`, and `StudentService` needs to save a student, how does it talk to `SchoolMasterContext` if the Application layer isn't allowed to reference the Infrastructure layer?"*

**Dependency Inversion!**

1. The `Application` layer creates an interface called `IStudentRepository` with a method `AddStudent(Student student)`.
2. The `StudentService` (in Application) asks the constructor for an `IStudentRepository`. It doesn't care *how* it works, it just trusts the contract.
3. The `Infrastructure` layer references the `Application` layer. It creates a `StudentRepository.cs` class that implements `IStudentRepository`, handling the actual `DbContext` save logic.
4. When the API boots up (`Program.cs`), it pieces them together: *"Hey StudentService, whenever you ask for an IStudentRepository, I will hand you a StudentRepository from Infrastructure."*

If you decide to rip out PostgreSQL tomorrow and replace it with MongoDB, you **only** have to touch the `Infrastructure` project. Your `Application` and `Domain` projects remain 100% untouched because they don't know what PostgreSQL is to begin with! That is the power of Clean Architecture.

---

## Your Refactored Codebase Structure

If you were to rewrite your current `SchoolMaster` repository into Clean Architecture, here is exactly how your files would be reorganized into the 4 distinct projects:

```text
SchoolMaster.Solution/
│
├── SchoolMaster.Domain/                     (Project 1 - Core)
│   ├── Models/
│   │   ├── Student.cs
│   │   ├── Staff.cs
│   │   └── User.cs
│   └── SchoolMaster.Domain.csproj           (No Dependencies)
│
├── SchoolMaster.Application/                (Project 2 - Business Logic)
│   ├── DTOs/
│   │   ├── AuthResponse.cs
│   │   ├── BaseResponse.cs
│   │   ├── RegisterRequest.cs
│   │   └── (All other DTOs...)
│   ├── Services/
│   │   ├── Interfaces/
│   │   │   ├── IAuthService.cs
│   │   │   ├── IStudentService.cs
│   │   │   └── IEmailService.cs         <- Even Email is defined here!
│   │   ├── AuthService.cs
│   │   └── StudentService.cs
│   ├── Repositories/
│   │   └── Interfaces/
│   │       ├── IAuthRepository.cs
│   │       └── IStudentRepository.cs       <- Concrete repos are hidden in Infrastructure
│   └── SchoolMaster.Application.csproj      (References SchoolMaster.Domain)
│
├── SchoolMaster.Infrastructure/             (Project 3 - Data & External Services)
│   ├── Data/
│   │   └── SchoolMasterContext.cs           <- EF Core lives here
│   ├── Repositories/
│   │   ├── AuthRepository.cs
│   │   └── StudentRepository.cs
│   ├── Services/
│   │   └── EmailService.cs              <- MailKit lives here
│   └── SchoolMaster.Infrastructure.csproj   (References SchoolMaster.Application)
│
└── SchoolMaster.Api/                        (Project 4 - Presentation)
    ├── Controllers/
    │   ├── AuthController.cs
    │   └── StudentsController.cs
    ├── Middleware/
    │   └── ExceptionMiddleware.cs
    ├── appsettings.json
    ├── Program.cs                       <- DI mapping connects Infrastructure to Application here
    ├── SchoolMaster.http
    ├── Dockerfile                       <- Containerizes the Api
    └── SchoolMaster.Api.csproj           (References Application & Infrastructure)
```

As you can see, the Application layer becomes incredibly rich. It dictates the business logic and all the core Interfaces. The Infrastructure layer becomes completely "dumb", containing only the physical implementations of how to save a database entity or send an email.
