# Attendance API — Implementation Guide

This document explains how the Attendance API works end to end — from a teacher submitting attendance in the frontend, all the way to a guardian receiving an email notification. It covers every class involved, why each one exists, and the design decisions behind them.

---

## Table of Contents

1. [What the Attendance API Does](#1-what-the-attendance-api-does)
2. [The Full Pipeline at a Glance](#2-the-full-pipeline-at-a-glance)
3. [Layer 1 — The Controller](#3-layer-1--the-controller)
4. [Layer 2 — The Service](#4-layer-2--the-service)
5. [Layer 3 — The Entity](#5-layer-3--the-entity)
6. [Layer 4 — Domain Events](#6-layer-4--domain-events)
7. [Layer 5 — Unit of Work](#7-layer-5--unit-of-work)
8. [Layer 6 — Event Handler](#8-layer-6--event-handler)
9. [Layer 7 — The Scheduler Abstraction](#9-layer-7--the-scheduler-abstraction)
10. [Layer 8 — The Background Job](#10-layer-8--the-background-job)
11. [Why Every Class Exists — One-liner Summary](#11-why-every-class-exists--one-liner-summary)
12. [Key Design Decisions and Tradeoffs](#12-key-design-decisions-and-tradeoffs)
13. [Things to Watch Out For](#13-things-to-watch-out-for)

---

## 1. What the Attendance API Does

The Attendance API lets teachers mark daily attendance for students in their class. Each submission:

- Creates one `DailyAttendance` record per student per day, anchored to the active academic term
- Supports corrections — re-submitting overwrites the existing record (upsert)
- Automatically queues an email notification to the guardian for every student newly marked absent
- Does not send duplicate notifications if an already-absent student is re-submitted as absent

There are three endpoints:

| Method | Route | Purpose |
|--------|-------|---------|
| `POST` | `/api/v1/attendance` | Mark attendance for a class |
| `GET` | `/api/v1/attendance/class/{classId}` | Get all records for a class on a date |
| `GET` | `/api/v1/attendance/student/{studentId}` | Get a student's attendance history |

---

## 2. The Full Pipeline at a Glance

```
Teacher submits attendance (POST /api/v1/attendance)
        │
        ▼
[AttendanceController]        ← receives HTTP request, checks permission
        │
        ▼
[AttendanceService]           ← validates, builds records
        │
        ├──► [AttendanceRepository]  stages new records (no DB write yet)
        │
        ▼
[UnitOfWork.SaveChangesAsync()]
        │
        ├──► Commits all records to the database
        │
        └──► Dispatches domain events via MediatR
                    │
                    ▼
        [StudentMarkedAbsentEventHandler]
                    │
                    ▼
        [HangfireAttendanceJobScheduler]   ← enqueues a background job to PostgreSQL
                    │
                    ▼ (runs later, outside the HTTP request)
        [AbsenceNotificationJob]           ← sends email to the student's guardian
```

The teacher's browser gets a response immediately. The email is sent in the background.

---

## 3. Layer 1 — The Controller

**File:** `src/Api/Controllers/AttendanceController.cs`

The controller is the HTTP front door. It does three things only:

1. Checks that the caller has the correct permission (`AttendanceMark`, `AttendanceViewClass`, `AttendanceViewStudent`)
2. Passes the request to the service
3. Returns the result as JSON

It contains no business logic. No database calls. No email logic. If you find yourself writing an `if` statement in a controller, it belongs in the service instead.

```csharp
[HasPermission(Permission.AttendanceMark)]
[HttpPost]
public async Task<ActionResult<BaseResponse<MarkAttendanceResponse>>> MarkAttendance(
    [FromBody] MarkAttendanceRequest request)
{
    var result = await attendanceService.MarkAttendanceAsync(request);
    return Ok(result);
}
```

---

## 4. Layer 2 — The Service

**File:** `src/Application/Services/AttendanceService.cs`

This is where all the "does this make sense?" logic lives. It runs these steps in order:

### Step 1 — Anchor to the active term

```csharp
var currentYear = await _academicYearRepo.GetCurrentAsync()
    ?? throw new AcademicYearNotFoundException("...");

var currentTerm = await _academicYearRepo.GetCurrentTermAsync(currentYear.Id)
    ?? throw new TermNotFoundException("...");
```

Attendance must be tied to a specific term at the moment of marking. We store `TermId` explicitly rather than deriving it from date ranges later. Why? Because term dates can be edited retrospectively — if you derive the term from dates after the fact, old records could silently reassign to a different term. Storing `TermId` at marking time makes the record immutable with respect to term.

### Step 2 — Validate the class and all student IDs

```csharp
var classStudents = await _studentRepo.GetStudentsByClassIdAsync(request.ClassId);
var validStudentIds = classStudents.Select(s => s.Id).ToHashSet();

foreach (var entry in request.Records)
{
    if (!validStudentIds.Contains(entry.StudentId))
        throw new StudentNotInClassException(...);
}
```

All students are validated **before any records are written**. This is intentional. If validation and writing were combined into one loop, the first few students could be saved before a later one fails — leaving the data in a partial state. Two loops ensure the entire request is either accepted or rejected as a whole.

The server always validates student membership even though the frontend loaded the student list from the class endpoint first. The reason: the API is a public contract. Any HTTP client with a valid JWT can send arbitrary student IDs. Frontend behaviour is a UI concern, not a security guarantee.

### Step 3 — Upsert logic

```csharp
var existing = await _attendanceRepo.GetByClassAndDateAsync(request.ClassId, request.Date);
var existingByStudent = existing.ToDictionary(r => r.StudentId);

foreach (var entry in request.Records)
{
    if (existingByStudent.TryGetValue(entry.StudentId, out var existingRecord))
        existingRecord.UpdateStatus(entry.Status, teacherId, entry.Notes);  // correction
    else
        toCreate.Add(DailyAttendance.Create(...));                           // first-time mark
}
```

Teachers make mistakes. Upsert lets them resubmit the same class and date to correct a record without hitting a duplicate key error.

### Step 4 — Count notifications from the entities

```csharp
var notificationsQueued = toCreate.Concat(updatedExisting)
    .Sum(r => r.DomainEvents.Count);
```

The service does **not** re-implement the condition for when a notification fires. It simply counts how many domain events the entities raised. The entity is the source of truth for that rule. If the entity's condition ever changes, the counter stays correct automatically.

---

## 5. Layer 3 — The Entity

**File:** `src/Domain/Entities/DailyAttendance.cs`

`DailyAttendance` is not just a data bag — it has behaviour. It owns the rule "a new absence means a notification should fire."

### `Create()` — factory method

```csharp
public static DailyAttendance Create(...)
{
    var record = new DailyAttendance { ... };

    if (status == AttendanceStatus.Absent)
        record._domainEvents.Add(new StudentMarkedAbsentEvent(...));

    return record;
}
```

When a new record is created as `Absent`, the entity adds a domain event to its private list. Nothing fires yet. The event sits there until the Unit of Work dispatches it after the database write.

### `UpdateStatus()` — for corrections

```csharp
public void UpdateStatus(AttendanceStatus status, Guid markedByTeacherId, string? notes)
{
    var wasAbsent = Status == AttendanceStatus.Absent;
    Status = status;
    // ...
    if (status == AttendanceStatus.Absent && !wasAbsent)
        _domainEvents.Add(new StudentMarkedAbsentEvent(...));
}
```

Only adds the event if this is a **new** absence. If the student was already absent and the teacher resubmits as absent again, no duplicate notification fires. This logic lives in the entity, not in the service, because it is a business rule about the entity's own state.

All properties have `private set` — nothing outside the entity can change them directly. State changes only happen through `Create()` and `UpdateStatus()`. This prevents accidental mutation from anywhere in the codebase.

---

## 6. Layer 4 — Domain Events

**Files:**
- `src/Domain/Common/IDomainEvent.cs`
- `src/Domain/Common/IHasDomainEvents.cs`
- `src/Domain/Events/StudentMarkedAbsentEvent.cs`

### `IDomainEvent`

```csharp
public interface IDomainEvent : INotification { }
```

A domain event represents something that **happened** — a fact in the past. By extending MediatR's `INotification`, any class that implements `IDomainEvent` can be published through MediatR and any registered handler will receive it automatically.

### `IHasDomainEvents`

```csharp
public interface IHasDomainEvents
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
```

This is a contract that any entity can implement to say "I may carry pending events." `DailyAttendance` implements it. The Unit of Work uses this interface to collect events from all tracked entities without needing to know which specific entity types raised them.

### `StudentMarkedAbsentEvent`

```csharp
public record StudentMarkedAbsentEvent(Guid TenantId, Guid StudentId, Guid ClassId, DateOnly Date)
    : IDomainEvent;
```

A C# `record` — immutable, just data. It carries the minimum facts the handler needs to schedule the notification job: which tenant, which student, which date.

---

## 7. Layer 5 — Unit of Work

**File:** `src/Infrastructure/Services/UnitOfWork.cs`

The Unit of Work is the **conductor**. It controls exactly when the database is written and when events are dispatched.

```csharp
public async Task SaveChangesAsync()
{
    // 1. Collect entities that carry pending events
    var entityEntries = _context.ChangeTracker
        .Entries<IHasDomainEvents>()
        .Where(e => e.Entity.DomainEvents.Count > 0)
        .ToList();

    var events = entityEntries.SelectMany(e => e.Entity.DomainEvents).ToList();

    // 2. Clear BEFORE the write so a re-entrant save cannot re-collect the same events
    foreach (var entry in entityEntries)
        entry.Entity.ClearDomainEvents();

    // 3. Commit everything to the database
    await _context.SaveChangesAsync();

    // 4. Dispatch events AFTER the commit
    foreach (var domainEvent in events)
        await _mediator.Publish(domainEvent);
}
```

The ordering is deliberate:

- Events are dispatched **after** the database write succeeds. If the database fails, no notification job is enqueued. You will never send a guardian an email for attendance that was never actually saved.
- Events are **cleared before** the write so that if a handler triggers another save cycle, the same events are not dispatched twice.

**Delivery guarantee, stated honestly.** The attendance commit (step 3) and the Hangfire enqueue (inside step 4) run on **separate database transactions**, even though both hit the same PostgreSQL instance. If the process dies in the narrow window between them, the attendance is saved but the notification is never queued. This is an at-least-effort guarantee, not exactly-once. For a single-database monolith this window is acceptable; a true transactional outbox would close it but is not justified at this stage. Do not describe this pipeline as an outbox.

---

## 8. Layer 6 — Event Handler

**File:** `src/Infrastructure/EventHandlers/StudentMarkedAbsentEventHandler.cs`

MediatR delivers the event here automatically after the Unit of Work publishes it.

```csharp
public class StudentMarkedAbsentEventHandler(IAttendanceJobScheduler scheduler)
    : INotificationHandler<StudentMarkedAbsentEvent>
{
    public Task Handle(StudentMarkedAbsentEvent notification, CancellationToken cancellationToken)
    {
        scheduler.ScheduleAbsenceNotification(
            notification.TenantId, notification.StudentId, notification.Date);
        return Task.CompletedTask;
    }
}
```

The handler does **one thing** — it tells the scheduler to enqueue a job. It does not send an email. It does not look up the student. Keeping the handler thin means the HTTP request stays fast: the teacher's browser gets a response without waiting for any email infrastructure.

This class lives in Infrastructure because, while it uses the Application-layer `IAttendanceJobScheduler` interface, it is the concrete wire between a MediatR event and a Hangfire job — both infrastructure concerns.

---

## 9. Layer 7 — The Scheduler Abstraction

**Files:**
- `src/Application/Services/Interfaces/IAttendanceJobScheduler.cs`
- `src/Infrastructure/Jobs/HangfireAttendanceJobScheduler.cs`
- `src/Infrastructure/Jobs/NoOpAttendanceJobScheduler.cs`

### Why three files for one job?

The Application layer must not depend on Hangfire. Application defines the *what* (`IAttendanceJobScheduler`), Infrastructure provides the *how*.

**`IAttendanceJobScheduler`** — Application layer interface

```csharp
public interface IAttendanceJobScheduler
{
    void ScheduleAbsenceNotification(Guid tenantId, Guid studentId, DateOnly date);
}
```

The event handler depends on this interface, not on Hangfire. Application stays clean.

**`HangfireAttendanceJobScheduler`** — real implementation

```csharp
client.Enqueue<IAbsenceNotificationJob>(job => job.SendAsync(tenantId, studentId, date));
```

One line. Hangfire serializes the arguments to JSON and persists the job to PostgreSQL immediately. The job is guaranteed to run even if the server restarts before it executes.

**`NoOpAttendanceJobScheduler`** — test implementation

```csharp
public void ScheduleAbsenceNotification(Guid tenantId, Guid studentId, DateOnly date) { }
```

Empty on purpose. Tests do not want a Hangfire server running. `Program.cs` registers this implementation when the app is in test mode. The event fires, the handler calls this, nothing happens — tests pass cleanly.

---

## 10. Layer 8 — The Background Job

**File:** `src/Infrastructure/Jobs/AbsenceNotificationJob.cs`

This runs **outside the HTTP request**, in a separate Hangfire worker thread, potentially seconds or minutes after attendance was marked.

```csharp
public async Task SendAsync(Guid tenantId, Guid studentId, DateOnly date)
{
    var student = await studentRepo.GetStudentByIdIgnoringFiltersAsync(studentId, tenantId);
    if (student is null) return;

    var tenant = await tenantRepo.GetByIdAsync(tenantId);
    var schoolName = tenant?.Name ?? "SchoolMaster";
    // build email and send...
}
```

Two things to notice:

**Sequential queries, not parallel.** It is tempting to run the student and tenant lookups at the same time with `Task.WhenAll` since neither needs the other's result. Do not. Both repositories share this job's single scoped `DbContext`, and EF Core forbids two operations running concurrently on one context instance. It throws "a second operation was started on this context before a previous operation completed." The failure is a race, so it can pass under light load and fail under heavy load, which makes it especially dangerous. Real parallel queries would require separate `DbContext` instances via `IDbContextFactory`, which is not worth it for two cheap lookups. The same rule applies in `AttendanceService` when looking up the class and the class roster.

**`IgnoreQueryFilters` variant for the student lookup.** There is no `HttpContext` in a Hangfire job, which means `ICurrentTenant.Id` would return `Guid.Empty`. The global EF Core query filter on `Student` uses `ICurrentTenant.Id`, so a normal query would return nothing. `GetStudentByIdIgnoringFiltersAsync` bypasses the global filter and passes `tenantId` explicitly instead, maintaining tenant isolation without relying on the HTTP context.

**Silent return if student is null.** A student could be withdrawn from the school between the moment attendance was marked and the moment the job runs. Throwing an exception here would cause Hangfire to retry the job indefinitely. Returning silently is the correct behaviour — the attendance record is still saved, the job just has nothing to do.

---

## 11. Why Every Class Exists — One-liner Summary

| File | Purpose |
|------|---------|
| `AttendanceController` | HTTP entry point — permission check, delegate to service, return result |
| `AttendanceService` | All business rules: term resolution, student validation, upsert logic |
| `DailyAttendance` | Owns the attendance record and the rule "absent = raise an event" |
| `IDomainEvent` / `IHasDomainEvents` | Contracts that let UnitOfWork collect events from any entity |
| `StudentMarkedAbsentEvent` | The immutable fact that was raised — carries data the handler needs |
| `UnitOfWork` | Ensures events are dispatched only after the DB write succeeds |
| `StudentMarkedAbsentEventHandler` | Bridges the domain event to the job scheduler |
| `IAttendanceJobScheduler` | Interface so Application layer stays free of Hangfire |
| `HangfireAttendanceJobScheduler` | Real implementation — persists a background job to PostgreSQL |
| `NoOpAttendanceJobScheduler` | Test implementation — does nothing so tests need no Hangfire server |
| `AbsenceNotificationJob` | Sends the actual email to the guardian, runs in background |

---

## 12. Key Design Decisions and Tradeoffs

### Why not send the email directly in the service?

Three reasons:

1. **Speed.** The teacher would wait for the email server to respond before their browser got a result. Email delivery is slow and can time out.
2. **Reliability.** If the email server is down at the moment of marking, the notification is lost forever. With Hangfire, the job persists to PostgreSQL and retries automatically on failure.
3. **Separation of concerns.** The service's job is to record attendance correctly. Notification is a side effect. Mixing them makes both harder to test and change independently.

### Why store `TermId` explicitly instead of deriving it from dates?

Term boundaries can be edited retroactively by an admin. If you derive the term from date ranges at query time, changing a term's end date silently reassigns old attendance records to a different term. Storing `TermId` at the moment of marking makes the record's academic context immutable.

### Why does the entity own the "absent = notify" rule?

Because it is a fact about the entity's own state transition. The service does not need to know the condition — it just reads how many domain events the entity raised. If the condition ever changes (say, `Late` also triggers a notification), only `DailyAttendance.cs` needs to change. The service, handler, and job are unaffected.

### Why two validation loops in the service?

The first loop validates every student before the second loop writes anything. This ensures the entire request is either accepted or rejected as a whole. If validation and writing were in one loop, five students could be saved before a sixth fails — leaving the data in a partial state.

---

## 13. Things to Watch Out For

- **Never put business logic in the controller.** The controller only handles HTTP concerns.
- **Never call `SaveChangesAsync` in a repository.** That belongs to the Unit of Work boundary.
- **Never reference `HttpContext` in a Hangfire job.** There is no HTTP context in a background thread. Use `IgnoreQueryFilters()` with an explicit `TenantId` predicate instead.
- **Never skip server-side validation because the frontend "already loaded the right data."** The API is a public contract — any client can send any payload.
- **Dispatch events after the DB commit, never before.** Events before commit means notifications fire for data that may never actually be saved.
- **New domain exceptions must be mapped in `ExceptionMiddleware`.** If you add a new exception class and do not add a case for it in the middleware, the API returns a generic 500 instead of the correct status code.
