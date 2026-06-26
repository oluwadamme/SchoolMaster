# Academic API — Implementation Guide

This document explains how the Academic API works, the decisions behind each design choice, and the tradeoffs involved. It is written for someone who is new to the codebase or wants to understand the "why" behind the code, not just the "what".

---

## Table of Contents

1. [What the Academic API Does](#1-what-the-academic-api-does)
2. [How the Data Is Structured](#2-how-the-data-is-structured)
3. [Clean Architecture — Why the Code Is Split Into Layers](#3-clean-architecture--why-the-code-is-split-into-layers)
4. [Multi-Tenancy — Each School Gets Its Own Data](#4-multi-tenancy--each-school-gets-its-own-data)
5. [Domain Entities — Private Setters and Factory Methods](#5-domain-entities--private-setters-and-factory-methods)
6. [The Singleton Active Record Pattern — IsCurrent](#6-the-singleton-active-record-pattern--iscurrent)
7. [Date Overlap Detection — The Math Behind It](#7-date-overlap-detection--the-math-behind-it)
8. [The Unit of Work Pattern — Why Repositories Don't Call SaveChanges](#8-the-unit-of-work-pattern--why-repositories-dont-call-savechanges)
9. [PATCH vs PUT — Partial Updates with Optional\<T\>](#9-patch-vs-put--partial-updates-with-optionalt)
10. [Period Types and Name Defaulting Logic](#10-period-types-and-name-defaulting-logic)
11. [Validation — Two Layers](#11-validation--two-layers)
12. [Error Handling and HTTP Status Codes](#12-error-handling-and-http-status-codes)
13. [Database Constraints as a Safety Net](#13-database-constraints-as-a-safety-net)
14. [Pagination](#14-pagination)
15. [Testing Strategy](#15-testing-strategy)
16. [Endpoint Reference](#16-endpoint-reference)

---

## 1. What the Academic API Does

The Academic API lets a school manage its core academic structure:

- **Academic Years** — e.g. "2025/2026". A school runs one academic year at a time.
- **Terms** — e.g. "First Term", within an academic year. A school runs one term at a time within a year.
- **Classes** — e.g. "JSS 1A". A class has an optional form teacher.
- **Subjects** — e.g. "Mathematics". A subject has an optional short code like "MTH".
- **Periods** — time slots in a class's weekly timetable, e.g. "Monday 08:00–09:00 — Mathematics".

These resources form a hierarchy:

```
Tenant (School)
└── Academic Year (2025/2026)
    └── Term (First Term)

Tenant (School)
└── Class (JSS 1A)
    └── Period (Monday 08:00-09:00, Mathematics)
        └── Subject (Mathematics)
```

Academic Years, Terms, Classes, and Subjects are independent resources. Periods link a Class to a Subject (via a time slot on a given day of the week).

---

## 2. How the Data Is Structured

### AcademicYear

```csharp
public class AcademicYear
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }  // which school owns this
    public string Name { get; private set; }     // "2025/2026"
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public bool IsCurrent { get; private set; }  // is this the active year?
    public ICollection<Term> Terms { get; private set; }
}
```

### Term

```csharp
public class Term
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid AcademicYearId { get; private set; }  // belongs to which year
    public string Name { get; private set; }          // "First Term"
    public int TermNumber { get; private set; }       // 1, 2, or 3 (for ordering)
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public bool IsCurrent { get; private set; }
}
```

### Class

```csharp
public class Class
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; }      // "JSS 1A"
    public Guid? FormTeacherId { get; private set; }  // optional — a Staff member ID
}
```

### Period

```csharp
public class Period
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ClassId { get; private set; }
    public Guid? SubjectId { get; private set; }   // null for non-timetabled periods
    public Guid? TeacherId { get; private set; }   // null for non-timetabled periods
    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public PeriodType Type { get; private set; }   // Timetabled, DailyRegister, NonAcademic
    public string Name { get; private set; }        // "Mathematics" or "Morning Register"
}
```

---

## 3. Clean Architecture — Why the Code Is Split Into Layers

The codebase follows Clean Architecture. Instead of putting all logic in one place, it is divided into layers:

```
src/
├── Domain/        ← The core rules. No dependencies on anything else.
├── Application/   ← Orchestrates use cases. Depends only on Domain.
├── Infrastructure/← Database, external services. Depends on Application.
└── Api/           ← HTTP controllers. Depends on Application.
```

**Why?**

- **Domain** holds the business entities (`AcademicYear`, `Term`, etc.) and domain exceptions. It knows nothing about databases or HTTP. This means the core rules can be tested in isolation and never change because of infrastructure decisions.
- **Application** holds the service (`AcademicService`) and the repository interfaces (`IAcademicYearRepository`). It defines *what* it needs from the database but not *how* it is done.
- **Infrastructure** holds the actual EF Core repository implementations. If you later swap PostgreSQL for another database, you only change this layer.
- **Api** holds the controllers. They receive HTTP requests, call the service, and return HTTP responses.

**Tradeoff**: More files and indirection compared to a simple controller-that-hits-the-DB approach. The payoff is testability — you can unit-test the service logic without a real database.

---

## 4. Multi-Tenancy — Each School Gets Its Own Data

SchoolMaster is a multi-tenant application. Multiple schools share the same database, but each school must only see its own data.

### How it works

Every entity has a `TenantId` column. The `TenantResolverMiddleware` reads the `X-Tenant-Subdomain` HTTP header, looks up the matching tenant, and stores its ID in `HttpContext.Items["TenantId"]`.

The `CurrentTenant` service reads this stored ID (with a fallback to the `tenant_id` JWT claim) and exposes it as `ICurrentTenant.Id`.

EF Core **global query filters** then automatically scope every DB query:

```csharp
// In SchoolMasterContext.OnModelCreating:
modelBuilder.Entity<AcademicYear>()
    .HasQueryFilter(a => a.TenantId == _currentTenant.Id);
```

This means every `SELECT` on `AcademicYears` automatically adds `WHERE TenantId = '<current school's ID>'`. Developers writing repository code do not need to remember to add this filter manually.

### The exception: IgnoreQueryFilters()

There is one case where the global filter is bypassed: `GetUserByIdAsync` in `UserRepository`. This method is called during the refresh-token flow. The refresh endpoint does not use `[Authorize]`, so `httpContext.User` carries no claims and the global filter would resolve to `TenantId = Guid.Empty`, making the lookup always fail.

The fix: call `IgnoreQueryFilters()` and add an explicit `&& x.TenantId == tenantId` predicate. The tenant ID is extracted from the (cryptographically signed) access token body, so isolation is still guaranteed — just enforced by the query predicate instead of the global filter.

**Rule**: Every call to `IgnoreQueryFilters()` must re-implement the same isolation guarantee explicitly. Never use it without a good reason documented in a comment.

---

## 5. Domain Entities — Private Setters and Factory Methods

All entity properties use `private set`:

```csharp
public string Name { get; private set; }
```

This means you cannot do this from outside the class:

```csharp
year.Name = "something"; // ❌ compile error
```

Instead, all mutations go through explicit methods:

```csharp
year.Update(newName, newStart, newEnd);  // ✅
year.SetAsCurrent();                     // ✅
```

**Why?**

If setters are public, any piece of code anywhere in the codebase can change the entity's state in unexpected ways. Private setters force all state changes to go through named methods, making it obvious what operations are valid and where business logic lives.

For the same reason, entities are never created with `new AcademicYear { Name = "..." }`. Instead they use a static factory method:

```csharp
var year = AcademicYear.Create(tenantId, name, start, end, isCurrent);
```

The factory method ensures the entity is always created in a valid, complete state with an ID and timestamp already set.

---

## 6. The Singleton Active Record Pattern — IsCurrent

Both `AcademicYear` and `Term` have an `IsCurrent` flag. The rule is: **at most one record per tenant can be current at any given time**.

### How promotion works

When you set a new record as current, the existing current one is automatically demoted:

```csharp
// In CreateAcademicYearAsync:
if (request.SetAsCurrent)
{
    var existing = await _academicYearRepository.GetCurrentAsync();
    if (existing != null)
    {
        existing.UnsetCurrent();           // demote the old one
        await _academicYearRepository.UpdateAsync(existing);
    }
}
```

### Why you cannot explicitly set IsCurrent = false via the API

The `UpdateAcademicYearRequest` has a `bool? SetAsCurrent` field. Only `true` is acted on. Sending `false` is a no-op.

This is intentional. If you could explicitly demote the current year without simultaneously promoting another, you would end up with no current year — an invalid state. The only valid state transitions are:

- No current year → promote one → one current year
- One current year → promote another → old one auto-demotes, new one is current

This is the same pattern used by systems like Stripe (current payment method) and Shopify (active store theme).

### Database enforcement

The application-level logic handles the normal case, but there is also a PostgreSQL **partial unique index** as a database-level guarantee:

```sql
-- Only one academic year per tenant can have IsCurrent = true
CREATE UNIQUE INDEX ON "AcademicYears" ("TenantId")
WHERE "IsCurrent" = true;
```

This means even if there were a race condition (two requests both trying to set a different year as current at the exact same moment), the database would reject one of them with a unique constraint violation. Defense in depth.

---

## 7. Date Overlap Detection — The Math Behind It

### Term date validation

When creating a term, two checks are performed:

**Check 1 — Containment**: The term must fit inside its academic year.

```csharp
if (request.StartDate < year.StartDate || request.EndDate > year.EndDate)
    throw new TermDateOutOfRangeException(...);
```

**Check 2 — No overlap with sibling terms**: Two terms in the same year must not share any dates.

```csharp
var hasOverlap = existingTerms.Any(t =>
    request.StartDate < t.EndDate && request.EndDate > t.StartDate);
```

This formula — `A_start < B_end AND A_end > B_start` — is the standard way to detect any overlap between two date/time intervals. Here is why it works:

Imagine two terms on a timeline: Term A and Term B.

The only times they do NOT overlap are:
- A ends before B starts: `A_end <= B_start`
- A starts after B ends: `A_start >= B_end`

So they DO overlap when neither of those is true:
```
NOT (A_end <= B_start OR A_start >= B_end)
= A_end > B_start AND A_start < B_end
```

The **strict inequalities** (`<` and `>`, not `<=` and `>=`) mean that adjacent terms are allowed. If Term 1 ends on March 31 and Term 2 starts on April 1, they are adjacent but not overlapping — this is the correct behaviour.

### Self-exclusion during updates

When updating a term's dates, the overlap check must exclude the term being updated from the comparison:

```csharp
var hasOverlap = siblings.Any(t =>
    t.Id != termId &&   // ← exclude self
    newStart < t.EndDate && newEnd > t.StartDate);
```

Without this, a term would always appear to overlap with itself when you try to update it.

### Period time conflict detection

The same formula is applied to period time slots. No two periods on the same day in the same class may have overlapping times:

```csharp
var hasConflict = existingPeriods.Any(p =>
    request.StartTime < p.EndTime && request.EndTime > p.StartTime);
```

---

## 8. The Unit of Work Pattern — Why Repositories Don't Call SaveChanges

All repositories stage changes to EF Core's change tracker but never call `SaveChangesAsync()` themselves:

```csharp
// AcademicYearRepository.cs
public async Task AddAsync(AcademicYear year)
{
    await _context.AcademicYears.AddAsync(year);
    // No SaveChangesAsync() here — intentional.
}
```

`SaveChangesAsync()` is called exactly once per HTTP request by the `UnitOfWorkMiddleware`, after the controller action has finished successfully.

**Why?**

Consider `CreateAcademicYearAsync`. If `SetAsCurrent = true`, the service:
1. Demotes the existing current year
2. Creates the new year

If step 1 called `SaveChangesAsync` and step 2 then threw an exception (e.g. duplicate name), the old year would already be demoted and the new one would not exist. The database would be in an invalid state.

With the Unit of Work pattern, both changes are staged in memory. `SaveChangesAsync` is only called when the entire operation succeeds. If anything fails, the transaction is simply never committed.

**Tradeoff**: Developers must remember never to call `SaveChangesAsync` inside a repository. A single accidental call will commit a partial transaction. The convention is enforced by code review rather than a compiler rule.

---

## 9. PATCH vs PUT — Partial Updates with Optional\<T\>

All update endpoints use HTTP `PATCH` (partial update), not `PUT` (full replacement). This means a client can update just the name of a class without having to resend the form teacher ID.

### The null ambiguity problem

For simple fields like `string? Name`, null means "field was not sent — keep existing":

```csharp
var newName = request.Name ?? cls.Name;  // if null, keep old name
```

But for nullable reference fields like `Guid? FormTeacherId`, null is ambiguous:
- Did the client omit the field (keep existing)?
- Did the client explicitly send `null` to clear the teacher assignment?

JSON gives you no way to distinguish these with a plain `Guid?`.

### The Optional\<T\> solution

Fields where null is a meaningful value are wrapped in `Optional<T>`:

```csharp
public record UpdateClassRequest(
    string? Name,
    Optional<Guid?> FormTeacherId  // ← wraps a nullable Guid
);
```

`Optional<T>` is a struct with two states:

| State | Meaning | `HasValue` | `Value` |
|-------|---------|-----------|---------|
| Field absent from JSON | Keep existing | `false` | irrelevant |
| Field sent as `null` | Clear the value | `true` | `null` |
| Field sent as a UUID | Set to this ID | `true` | the UUID |

The service uses it like this:

```csharp
var newFormTeacherId = request.FormTeacherId.HasValue
    ? request.FormTeacherId.Value   // use whatever the client sent (even null)
    : cls.FormTeacherId;            // client didn't send it — keep existing
```

This pattern is used on `FormTeacherId` (Class), `Code` (Subject), and `TeacherId` (Period).

A custom `OptionalConverterFactory` (a `JsonConverterFactory`) handles the JSON deserialization so that absent fields and explicit nulls are correctly distinguished at the HTTP layer.

---

## 10. Period Types and Name Defaulting Logic

There are three types of periods:

| Type | Subject required? | Teacher required? | Default name |
|------|------------------|------------------|-------------|
| `Timetabled` | Yes | Yes | Subject's name (if `Name` omitted) |
| `DailyRegister` | No | No | "Morning Register" (if `Name` omitted) |
| `NonAcademic` | No | No | **No default — client must supply a name** |

The defaulting logic lives in the service:

```csharp
if (request.Type == PeriodType.Timetabled)
{
    var subject = await _subjectRepo.GetByIdAsync(request.SubjectId!.Value) ?? throw ...;
    periodName = request.Name ?? subject.Name;  // default to subject name
}
else if (request.Type == PeriodType.DailyRegister)
{
    periodName = request.Name ?? "Morning Register";
}
else  // NonAcademic
{
    periodName = request.Name!;  // validator guarantees this is not null
}
```

The validator enforces this at the HTTP boundary before the service is even called:

```csharp
When(x => x.Type == PeriodType.NonAcademic, () =>
{
    RuleFor(x => x.Name).NotEmpty()
        .WithMessage("Name is required for non-academic periods (e.g. Break, Lunch, Prep).");
});
```

**Tradeoff**: The service logic for name resolution is moderately complex. The benefit is a friendlier API — for the common case (a Timetabled period), the client does not have to repeat the subject name.

---

## 11. Validation — Two Layers

Validation happens in two places:

### Layer 1 — FluentValidation (HTTP boundary)

Request DTOs have validator classes. These are registered with the DI container and run automatically before the controller action is called. They catch obvious problems early:

- Required fields not provided
- String lengths exceeded
- End date before start date
- `TeacherId` missing for `Timetabled` periods

A validation failure returns **400 Bad Request** with a list of error messages.

### Layer 2 — Service logic (business rules)

Some rules require a database check and cannot be done by a simple validator:

- Does a year with this name already exist? (`DuplicateAcademicYearException` → 409 Conflict)
- Does this term's date range overlap another term? (`TermDateOverlapException` → 409 Conflict)
- Does this period's time slot conflict with another period? (`PeriodTimeConflictException` → 409 Conflict)
- Does this academic year ID actually exist? (`AcademicYearNotFoundException` → 404 Not Found)

These throw domain exceptions, which the `ExceptionMiddleware` converts to the correct HTTP status code.

---

## 12. Error Handling and HTTP Status Codes

The `ExceptionMiddleware` sits at the top of the request pipeline. Any unhandled exception is caught here, matched against a list of known domain exceptions, and converted to the correct HTTP response. Controllers never need to write try/catch blocks.

| Exception | HTTP Status |
|-----------|------------|
| `AcademicYearNotFoundException` | 404 Not Found |
| `TermNotFoundException` | 404 Not Found |
| `ClassNotFoundException` | 404 Not Found |
| `SubjectNotFoundException` | 404 Not Found |
| `PeriodNotFoundException` | 404 Not Found |
| `DuplicateAcademicYearException` | 409 Conflict |
| `DuplicateClassNameException` | 409 Conflict |
| `DuplicateSubjectCodeException` | 409 Conflict |
| `TermDateOverlapException` | 409 Conflict |
| `TermDateOutOfRangeException` | 422 Unprocessable Entity |
| `PeriodTimeConflictException` | 409 Conflict |

**Why a separate middleware instead of try/catch in each controller?**

Putting error handling in one place means the mapping from exception to status code is defined once. If you add a new exception type, you update the middleware in one location. No controller needs to be changed.

---

## 13. Database Constraints as a Safety Net

Application-level checks (service logic) handle the normal case, but they have a race condition weakness: two concurrent requests can both pass the overlap check and both try to write, resulting in duplicate or overlapping data.

Database unique constraints are the last line of defence:

```csharp
// In SchoolMasterContext.OnModelCreating:

// No two academic years in the same school can have the same name
modelBuilder.Entity<AcademicYear>()
    .HasIndex(y => new { y.TenantId, y.Name }).IsUnique();

// No two terms in the same year can have the same term number
modelBuilder.Entity<Term>()
    .HasIndex(t => new { t.TenantId, t.AcademicYearId, t.TermNumber }).IsUnique();

// At most one current academic year per school (partial unique index)
modelBuilder.Entity<AcademicYear>()
    .HasIndex(y => y.TenantId)
    .IsUnique()
    .HasFilter("\"IsCurrent\" = true");

// At most one current term per academic year per school (partial unique index)
modelBuilder.Entity<Term>()
    .HasIndex(t => new { t.TenantId, t.AcademicYearId })
    .IsUnique()
    .HasFilter("\"IsCurrent\" = true");
```

The **partial unique index** (the `WHERE "IsCurrent" = true` filter) is a PostgreSQL feature that applies a unique constraint only to rows matching a condition. It means "within the rows where `IsCurrent = true`, the `TenantId` must be unique" — without restricting rows where `IsCurrent = false` at all.

**Accepted risk**: A database-level constraint for period time conflicts was not added because time overlap detection requires range comparison logic (`A_start < B_end AND A_end > B_start`) which is more complex to express as a database constraint. For this domain, concurrent period creation conflicts are rare and low-risk (school timetable setup is not a high-concurrency operation), so the application-level check is considered sufficient.

---

## 14. Pagination

The three list endpoints (academic years, classes, subjects) support pagination to avoid returning unbounded result sets.

Pagination parameters are extracted from the query string using a shared `PaginationRequest` record:

```csharp
public record PaginationRequest(int Page = 1, int PageSize = 20);
```

The defaults (page 1, 20 items) mean clients can call `GET /api/v1/academic/years` without any query parameters and get a sensible response.

Validation is done by `PaginationRequestValidator`: page must be ≥ 1, page size must be between 1 and 100. This prevents clients from requesting page 0 (which would cause a negative `OFFSET`) or requesting 10,000 records at once.

The response includes metadata to help clients build pagination controls:

```json
{
  "items": [...],
  "page": 1,
  "pageSize": 20,
  "totalCount": 47,
  "totalPages": 3
}
```

---

## 15. Testing Strategy

The Academic API has two test suites.

### Unit Tests (`AcademicServiceTests.cs`) — 39 tests

These test `AcademicService` in complete isolation. All five repositories and `ICurrentTenant` are replaced with **Moq** fakes. No database is involved.

**What they test well**:
- Business rule logic (overlap detection, date validation, name defaulting)
- `Optional<T>` behaviour (absent vs explicit null)
- The singleton `IsCurrent` pattern (SetAsCurrent=false is a no-op)
- Every domain exception is thrown for the right input

**What they cannot test**:
- Whether the database constraints are correct
- Whether the HTTP layer (controller, middleware, validators) is wired up correctly
- Whether the EF Core queries produce the right SQL

### Integration Tests (`AcademicControllerTests.cs`) — 38 tests

These start a real ASP.NET Core application (using `WebApplicationFactory`) and a real PostgreSQL database (in a Docker container, via Testcontainers). Each request goes through the full middleware pipeline.

**What they test well**:
- Correct HTTP status codes (201, 400, 404, 409, 422, 401)
- FluentValidation enforcement at the HTTP boundary
- Tenant isolation (two separate schools cannot see each other's data)
- `Optional<T>` JSON deserialization working end-to-end
- The full happy path for every CRUD operation

**Tradeoff**: Integration tests are slower (they spin up Docker) and require Docker to be running. Unit tests run instantly. The two suites complement each other — neither alone is sufficient.

---

## 16. Endpoint Reference

All endpoints require a valid JWT (`Authorization: Bearer <token>`) and the `AcademicManage` permission, except where noted.

### Academic Years

| Method | Path | Description | Response |
|--------|------|-------------|----------|
| `POST` | `/api/v1/academic/years` | Create an academic year | 201 `AcademicYearResponse` |
| `GET` | `/api/v1/academic/years` | List academic years (paginated) | 200 `PagedResponse<AcademicYearResponse>` |
| `PATCH` | `/api/v1/academic/years/{yearId}` | Partially update an academic year | 200 `AcademicYearResponse` |

### Terms

| Method | Path | Description | Response |
|--------|------|-------------|----------|
| `POST` | `/api/v1/academic/terms` | Create a term | 201 `TermResponse` |
| `GET` | `/api/v1/academic/years/{yearId}/terms` | List terms for a year | 200 `List<TermResponse>` |
| `PATCH` | `/api/v1/academic/terms/{termId}` | Partially update a term | 200 `TermResponse` |

### Classes

| Method | Path | Description | Response |
|--------|------|-------------|----------|
| `POST` | `/api/v1/academic/classes` | Create a class | 201 `ClassResponse` |
| `GET` | `/api/v1/academic/classes` | List classes (paginated) | 200 `PagedResponse<ClassResponse>` |
| `PATCH` | `/api/v1/academic/classes/{classId}` | Partially update a class | 200 `ClassResponse` |

### Subjects

| Method | Path | Description | Response |
|--------|------|-------------|----------|
| `POST` | `/api/v1/academic/subjects` | Create a subject | 201 `SubjectResponse` |
| `GET` | `/api/v1/academic/subjects` | List subjects (paginated) | 200 `PagedResponse<SubjectResponse>` |
| `PATCH` | `/api/v1/academic/subjects/{subjectId}` | Partially update a subject | 200 `SubjectResponse` |

### Periods & Timetable

| Method | Path | Permission | Description | Response |
|--------|------|-----------|-------------|----------|
| `POST` | `/api/v1/academic/classes/{classId}/periods` | `AcademicManage` | Add a period to a class | 201 `PeriodResponse` |
| `PATCH` | `/api/v1/academic/classes/{classId}/periods/{periodId}` | `AcademicManage` | Update a period | 200 `PeriodResponse` |
| `GET` | `/api/v1/academic/classes/{classId}/timetable` | `AcademicViewTimetable` | Get full timetable for a class | 200 `TimetableResponse` |

The timetable endpoint uses the `AcademicViewTimetable` permission (not `AcademicManage`) so that teachers and students can view timetables without being able to modify academic setup.

---

## Key Decisions Summary

| Decision | Why | Tradeoff |
|----------|-----|---------|
| Private setters on entities | Force all mutations through named methods | More boilerplate than public setters |
| Unit of Work (no SaveChanges in repos) | Atomic multi-step operations | Devs must remember the convention |
| `Optional<T>` for nullable PATCH fields | Distinguish "absent" from "explicit null" | More complex than plain nullable |
| Singleton IsCurrent (no explicit false) | Prevent state with no current record | Cannot demote without promoting |
| Partial unique index for IsCurrent | DB-level guarantee against race conditions | PostgreSQL-specific feature |
| PATCH instead of PUT | Client only sends what changed | Requires merge logic in the service |
| Two-layer validation (FluentValidation + service) | Fast rejection of bad input + DB-aware checks | Logic is split across two places |
| Application-level overlap check for periods | Simple to implement | Not race-condition proof (accepted risk) |
