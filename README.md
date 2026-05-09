# SchoolMaster
---

## Phase 1 — Core Foundation

> Weeks 1–4 · Clean Architecture scaffold, Identity & Access, Student Management, Teacher & Staff Management, Academic Setup, Attendance Tracking

---

### Phase 1 Features

- **Multi-Role Authentication** — JWT + refresh token auth with rotation, BCrypt password hashing, OTP email verification, and password reset flow. Supports Admin, Teacher, Student, Parent, and Staff roles.
- **Role-Based Access Control (RBAC)** — Fine-grained permissions per role. Each endpoint enforces ownership and role boundaries. No cross-tenant data leakage.
- **Rate Limiting** — Fixed window rate limiter on all auth endpoints to prevent brute-force attacks.
- **Audit Logging** — Every write operation records who performed it, when, and what changed.
- **Student Enrollment** — Full admission workflow: personal info, guardian contacts, class assignment, unique student ID generation, and photo upload.
- **Bulk Student Import** — CSV upload endpoint for mass enrollment. Validates each row and returns a per-row success/failure report.
- **Academic Year & Term Setup** — Configurable academic calendar: years, terms, classes, streams, and subjects.
- **Teacher & Staff Profiles** — Qualifications, subject assignments, department mapping, and leave request workflow.
- **Timetable Builder** — Conflict-free class schedule generation: assigns teachers, subjects, and rooms per period.
- **Daily Attendance Marking** — Teachers mark attendance per class per period. Supports present, absent, late, and excused statuses.
- **Automated Absence Notifications** — Domain event triggers a Hangfire background job that sends email + push notification to parent within 5 seconds of absence being recorded.
- **Attendance Threshold Alerts** — Configurable alert when a student's attendance drops below a set percentage (default 75%).
- **Attendance Reports** — Filterable by student, class, date range, and status. Exportable as PDF.
- **N-Tier Architecture** — DB → Repository → Service → Controller pattern. All layers communicate through interfaces for a straightforward data flow and clear separation of concerns.
- **Multi-Tenant Design** — Every query is scoped to a `TenantId`. Global EF Core query filter ensures no school sees another school's data. Tenant resolved from JWT claim.
- **Standardized Responses** — All endpoints return a consistent `BaseResponse<T>` wrapper with `success`, `message`, and `data` fields.
- **Global Error Handling** — Centralized middleware maps domain exceptions to HTTP status codes.
- **Strongly-Typed Configuration** — JWT, email, and tenant settings bound to typed options classes.
- **Input Validation** — FluentValidation on all request DTOs with consistent error response shape.
- **Structured Logging** — Serilog with request/response logging and error context.
- **Async Operations** — All database and I/O operations fully asynchronous.
- **Automated Testing** — Unit tests (xUnit + Moq) for all service and command handler logic. Integration tests using `WebApplicationFactory` + Testcontainers (real PostgreSQL).
- **CI/CD** — GitHub Actions: build → test → push Docker image → deploy on every push to `develop` and PR to `main`.
- **Docker Support** — Multi-stage Dockerfile and `docker-compose.yml` with PostgreSQL and Redis side by side.

---

### Phase 1 Tech Stack

| Technology | Version | Purpose |
|---|---|---|
| .NET SDK | 10.0 | Runtime & framework |
| ASP.NET Core | 10.0 | Web API (Minimal APIs) |
| Entity Framework Core | 10.0 | ORM / data access |
| Npgsql (EF Core Provider) | 10.0 | PostgreSQL driver |
| PostgreSQL | 16+ | Primary relational database |
| Redis | 7+ | Distributed cache + SignalR backplane |
| FluentValidation | 11.x | Request validation |
| BCrypt.Net-Next | 4.x | Password hashing |
| MailKit | 4.x | SMTP email (OTP, notifications) |
| Hangfire | 1.8.x | Background jobs (notifications, imports) |
| Serilog | 3.x | Structured logging |
| JWT Bearer Authentication | 10.0 | Token-based auth |
| OpenAPI | 10.0 | API documentation |
| xUnit | 2.x | Testing framework |
| Moq | 4.x | Mocking for unit tests |
| Testcontainers | 3.x | Real PostgreSQL in integration tests |
| Microsoft.AspNetCore.Mvc.Testing | 10.0 | Integration test host |
| Docker | — | Containerization |
| GitHub Actions | — | CI/CD pipeline |

---

### Phase 1 Project Structure

### Architecture

The application follows an N-Tier architecture (Controller → Service → Repository → Database). 
- **Controllers** handle HTTP requests and routing.
- **Services** contain all the core business logic.
- **Repositories** abstract the Entity Framework Core data access and database operations.

---

### Phase 1 API Endpoints

#### Authentication API

Base URL: `/api/v1/auth`

| Method | Endpoint | Description | Auth Required |
|---|---|---|---|
| `POST` | `/api/v1/auth/register` | Register a new user (sends OTP) | ❌ No |
| `POST` | `/api/v1/auth/login` | Login — returns JWT + refresh token | ❌ No |
| `POST` | `/api/v1/auth/refresh-token` | Exchange refresh token for new JWT | ❌ No |
| `POST` | `/api/v1/auth/verify-email` | Verify email with OTP | ❌ No |
| `POST` | `/api/v1/auth/resend-verification` | Resend OTP email | ❌ No |
| `POST` | `/api/v1/auth/forgot-password` | Request password reset OTP | ❌ No |
| `POST` | `/api/v1/auth/reset-password` | Reset password with OTP | ❌ No |
| `POST` | `/api/v1/auth/logout` | Revoke refresh token | ✅ Yes |

#### Students API

Base URL: `/api/v1/students`

> 🔒 All endpoints require a valid JWT. Scoped to the authenticated school tenant.

| Method | Endpoint | Description | Roles |
|---|---|---|---|
| `POST` | `/api/v1/students` | Enroll a new student | Admin |
| `POST` | `/api/v1/students/bulk-import` | Bulk enroll from CSV | Admin |
| `GET` | `/api/v1/students` | List all students (paginated, filterable) | Admin, Teacher |
| `GET` | `/api/v1/students/{id}` | Get student profile | Admin, Teacher, Parent (own child) |
| `PUT` | `/api/v1/students/{id}` | Update student profile | Admin |
| `DELETE` | `/api/v1/students/{id}` | Withdraw student | Admin |
| `GET` | `/api/v1/students/{id}/history` | Academic history across years | Admin, Teacher |
| `POST` | `/api/v1/students/{id}/photo` | Upload student photo | Admin |

#### Staff API

Base URL: `/api/v1/staff`

| Method | Endpoint | Description | Roles |
|---|---|---|---|
| `POST` | `/api/v1/staff` | Create staff profile | Admin |
| `GET` | `/api/v1/staff` | List all staff | Admin |
| `GET` | `/api/v1/staff/{id}` | Get staff profile | Admin, Teacher (own) |
| `PUT` | `/api/v1/staff/{id}` | Update staff profile | Admin |
| `POST` | `/api/v1/staff/{id}/assign-subject` | Assign subject to teacher | Admin |
| `POST` | `/api/v1/staff/{id}/leave-request` | Submit a leave request | Teacher, Staff |
| `PUT` | `/api/v1/staff/leave-request/{id}/approve` | Approve or reject leave | Admin |

#### Academic API

Base URL: `/api/v1/academic`

| Method | Endpoint | Description | Roles |
|---|---|---|---|
| `POST` | `/api/v1/academic/years` | Create academic year | Admin |
| `GET` | `/api/v1/academic/years` | List academic years | Admin, Teacher |
| `POST` | `/api/v1/academic/terms` | Create term within a year | Admin |
| `POST` | `/api/v1/academic/classes` | Create a class | Admin |
| `POST` | `/api/v1/academic/subjects` | Create a subject | Admin |
| `POST` | `/api/v1/academic/timetable` | Generate timetable for a class | Admin |
| `GET` | `/api/v1/academic/timetable/{classId}` | Get class timetable | Admin, Teacher, Student |

#### Attendance API

Base URL: `/api/v1/attendance`

| Method | Endpoint | Description | Roles |
|---|---|---|---|
| `POST` | `/api/v1/attendance` | Mark attendance for a class | Teacher |
| `POST` | `/api/v1/attendance/bulk` | Bulk mark entire class | Teacher |
| `GET` | `/api/v1/attendance/student/{id}` | Get student attendance summary | Admin, Teacher, Parent |
| `GET` | `/api/v1/attendance/class/{id}` | Get class attendance for a date | Admin, Teacher |
| `GET` | `/api/v1/attendance/report` | Filtered attendance report | Admin |
| `GET` | `/api/v1/attendance/alerts` | Students below threshold | Admin, Teacher |

---

### Phase 1 Request & Response Examples

#### Enroll a Student

```http
POST /api/v1/students
Content-Type: application/json
Authorization: Bearer <admin-token>

{
  "firstName": "Amara",
  "lastName": "Okafor",
  "dateOfBirth": "2012-03-15",
  "gender": "Female",
  "classId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "guardianName": "Chukwuemeka Okafor",
  "guardianPhone": "+2348012345678",
  "guardianEmail": "c.okafor@example.com",
  "medicalNotes": "Mild asthma — has inhaler"
}
```

**Response** `201 Created`:
```json
{
  "success": true,
  "message": "Student enrolled successfully",
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa7",
    "studentId": "STU-2024-00142",
    "firstName": "Amara",
    "lastName": "Okafor",
    "classId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "className": "JSS 1A",
    "enrolledAt": "2026-04-13T10:00:00Z"
  }
}
```

#### Mark Class Attendance

```http
POST /api/v1/attendance
Content-Type: application/json
Authorization: Bearer <teacher-token>

{
  "classId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "date": "2026-04-13",
  "periodId": "period-1",
  "records": [
    { "studentId": "STU-2024-00142", "status": "Present" },
    { "studentId": "STU-2024-00143", "status": "Absent" },
    { "studentId": "STU-2024-00144", "status": "Late" }
  ]
}
```

**Response** `200 OK`:
```json
{
  "success": true,
  "message": "Attendance recorded. 1 absence notification queued.",
  "data": {
    "totalMarked": 3,
    "present": 2,
    "absent": 1,
    "late": 1,
    "notificationsQueued": 1
  }
}
```

> Absence notification is sent to the parent of STU-2024-00143 within 5 seconds via Hangfire background job.

#### Bulk Import Students (CSV)

```http
POST /api/v1/students/bulk-import
Content-Type: multipart/form-data
Authorization: Bearer <admin-token>

file: students.csv
classId: 3fa85f64-5717-4562-b3fc-2c963f66afa6
```

**Response** `200 OK`:
```json
{
  "success": true,
  "message": "Bulk import completed",
  "data": {
    "totalRows": 45,
    "successful": 43,
    "failed": 2,
    "failures": [
      { "row": 12, "reason": "Guardian email is invalid" },
      { "row": 31, "reason": "Date of birth is required" }
    ]
  }
}
```

---

### Phase 1 Configuration

| Setting | Location | Description |
|---|---|---|
| Connection string | `appsettings.json` → `ConnectionStrings.DefaultConnection` | PostgreSQL connection |
| Redis | `appsettings.json` → `ConnectionStrings.Redis` | Cache + Hangfire storage |
| JWT secret | `appsettings.json` → `Jwt.Key` | Min 32 characters |
| JWT expiry | `appsettings.json` → `Jwt.ExpirationInMinutes` | Default: 60 |
| Refresh token expiry | `appsettings.json` → `Jwt.RefreshTokenExpirationInDays` | Default: 30 |
| SMTP server | `appsettings.json` → `EmailSettings.SmtpServer` | e.g. smtp.gmail.com |
| OTP expiry | `appsettings.json` → `EmailVerification.ExpirationInMinutes` | Default: 15 |
| Blob storage | `appsettings.json` → `BlobStorage.ConnectionString` | Azure Blob or S3 |
| Attendance threshold | `appsettings.json` → `Attendance.AlertThresholdPercent` | Default: 75 |

---

### Quick Start (Docker & Migrations)

**1. Run the application with Docker**
The application is fully dockerized with a PostgreSQL database and Redis cache. To start everything:
```bash
docker compose up --build -d
```
*The API will be available at `http://localhost:7001`.*

**2. Database Migrations**
To create a new EF Core migration after changing your entities:
```bash
dotnet ef migrations add <MigrationName>
```

To apply the migrations, simply rebuild and restart the API container (the application automatically applies pending migrations on startup):
```bash
docker compose up --build -d api
```

---

### Phase 1 Potential Improvements

- [] JWT + refresh token auth with rotation
- [] OTP email verification and password reset
- [] Multi-tenant architecture with TenantId query filter
- [] RBAC with fine-grained role permissions
- [] N-Tier Architecture (DB → Repo → Service → Controller)
- [] Global error handling middleware
- [] FluentValidation on all request DTOs
- [] Hangfire background jobs for notifications
- [] Attendance domain events → notification pipeline
- [] Unit and integration tests with Testcontainers
- [] Docker + docker-compose with PostgreSQL and Redis
- [] GitHub Actions CI/CD
- [ ] Audit log — queryable history of all write operations
- [ ] Timetable conflict detection — automatic validation before saving
- [ ] Attendance analytics — heatmap view per class per week
- [ ] Parent mobile push notifications via FCM (Firebase)
- [ ] CSV export for all attendance reports
- [ ] Soft delete — deactivate students/staff instead of hard delete
- [ ] API versioning — /api/v1/ prefix enforced from day one