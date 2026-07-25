# RBAC with Fine-Grained Role Permissions

## The Problem with Simple Role Checks

Right now, SchoolMaster uses a single `[Authorize(Roles = "Admin")]` attribute on one endpoint. That tells ASP.NET Core "the user must be an Admin" — nothing more.

This works for one endpoint. It breaks down fast. Imagine 40 endpoints across Students, Staff, Attendance, and Academic. Some are Admin-only. Some are Teacher-only. Some are shared. Some depend on ownership (a Parent can read their own child's record, but nobody else's).

With role string checks you end up writing things like:

```csharp
[Authorize(Roles = "Admin,Teacher,Parent")]
```

Now add a new role. You must audit every `[Authorize]` attribute in the entire codebase to decide whether the new role belongs there. That is not maintainable.

## What Fine-Grained RBAC Gives You

**Fine-grained RBAC** introduces a **Permission** as a first-class concept. Instead of checking "what role does this user have?", you check "does this user have permission to perform this specific action?"

Roles become collections of permissions. A Teacher role gets `AttendanceMark`, `StudentsRead`, `AcademicViewTimetable`. An Admin gets all of those plus `StudentsCreate`, `StudentsDelete`, `StaffApproveLeave`, etc.

When you add a new endpoint, you assign it one permission. You never touch role definitions again.

```
User logs in
    → JwtService reads their UserRole
    → Looks up RolePermissions[role] → e.g. [AttendanceMark, StudentsRead]
    → Encodes each permission as a "permission" claim in the JWT

Later: request hits POST /api/v1/attendance
    → [HasPermission(Permission.AttendanceMark)] fires
    → HasPermissionHandler reads "permission" claims from the JWT
    → Checks whether AttendanceMark is present
    → ✅ Allow  /  ❌ 403 Forbidden — zero database calls
```

The permissions travel inside the JWT. No database round-trip on every request to decide whether to proceed.

---

## Architecture Layers Touched (Inside Out)

| Layer | What changes |
|---|---|
| **Domain** | `Permission` enum, `RolePermissions` static map, `Staff` added to `UserRole` |
| **Infrastructure** | `JwtService.GenerateAccessToken` stamps permission claims into the token |
| **API** | `HasPermissionRequirement`, `HasPermissionHandler`, `HasPermissionAttribute`, policy registration in `Program.cs` |
| **Controllers** | Replace `[Authorize(Roles = "...")]` with `[HasPermission(Permission.X)]` |

---

## Step 1: Domain Layer

### Add Staff to UserRole

The current enum has Admin, Teacher, Student, Parent. Non-teacher staff (Accountants, Librarians) have no authentication role. `StaffRole` describes their job title inside the `Staff` entity — `UserRole` is what they authenticate as.

```csharp
// src/Domain/Enums/UserRole.cs
namespace SchoolMaster.Domain.Enums;

public enum UserRole
{
    Admin,
    Teacher,
    Student,
    Parent,
    Staff
}
```

### Define the Permission Enum

Each value is one discrete action. Naming convention: `ResourceAction`.

```csharp
// src/Domain/Enums/Permission.cs
namespace SchoolMaster.Domain.Enums;

public enum Permission
{
    // Students
    StudentsCreate,
    StudentsRead,
    StudentsUpdate,
    StudentsDelete,
    StudentsBulkImport,
    StudentsReadHistory,
    StudentsUploadPhoto,

    // Staff
    StaffCreate,
    StaffRead,
    StaffUpdate,
    StaffAssignSubject,
    StaffSubmitLeave,
    StaffApproveLeave,

    // Academic
    AcademicManage,
    AcademicViewTimetable,

    // Attendance
    AttendanceMark,
    AttendanceBulkMark,
    AttendanceViewStudent,
    AttendanceViewClass,
    AttendanceViewReport,
    AttendanceViewAlerts
}
```

### Define the RolePermissions Map

This is the single source of truth for "what can each role do?" It lives in Domain because it is a pure business rule — no HTTP, no database, no ASP.NET.

```csharp
// src/Domain/Authorization/RolePermissions.cs
using SchoolMaster.Domain.Enums;

namespace SchoolMaster.Domain.Authorization;

public static class RolePermissions
{
    private static readonly Dictionary<UserRole, IReadOnlyList<Permission>> _map = new()
    {
        [UserRole.Admin] = new[]
        {
            Permission.StudentsCreate,
            Permission.StudentsRead,
            Permission.StudentsUpdate,
            Permission.StudentsDelete,
            Permission.StudentsBulkImport,
            Permission.StudentsReadHistory,
            Permission.StudentsUploadPhoto,
            Permission.StaffCreate,
            Permission.StaffRead,
            Permission.StaffUpdate,
            Permission.StaffAssignSubject,
            Permission.StaffApproveLeave,
            Permission.AcademicManage,
            Permission.AcademicViewTimetable,
            Permission.AttendanceViewStudent,
            Permission.AttendanceViewClass,
            Permission.AttendanceViewReport,
            Permission.AttendanceViewAlerts
        },

        [UserRole.Teacher] = new[]
        {
            Permission.StudentsRead,
            Permission.StudentsReadHistory,
            Permission.StaffRead,
            Permission.AcademicViewTimetable,
            Permission.AttendanceMark,
            Permission.AttendanceBulkMark,
            Permission.AttendanceViewStudent,
            Permission.AttendanceViewClass,
            Permission.AttendanceViewAlerts
        },

        [UserRole.Student] = new[]
        {
            Permission.AcademicViewTimetable
        },

        [UserRole.Parent] = new[]
        {
            Permission.StudentsRead,
            Permission.AttendanceViewStudent
        },

        [UserRole.Staff] = new[]
        {
            Permission.StaffRead,
            Permission.StaffSubmitLeave
        }
    };

    public static IReadOnlyList<Permission> For(UserRole role)
    {
        return _map.TryGetValue(role, out var permissions)
            ? permissions
            : Array.Empty<Permission>();
    }
}
```

> [!NOTE]
> `Parent` gets `StudentsRead` and `AttendanceViewStudent`. That does **not** mean they see all students. The permission grants access to the endpoint. The ownership check (parent can only fetch their own child) is enforced in the service layer. These are two separate concerns — do not conflate them.

---

## Step 2: Infrastructure Layer — Stamp Permissions into the JWT

Update `JwtService.GenerateAccessToken` to read the user's permissions from `RolePermissions` and add each one as an individual claim.

```csharp
// src/Infrastructure/Services/JwtService.cs

using SchoolMaster.Domain.Authorization;  // add this

public string GenerateAccessToken(User user)
{
    var key = Encoding.ASCII.GetBytes(_jwtOptions.Key);

    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.GivenName, user.FirstName),
        new Claim(ClaimTypes.Surname, user.LastName),
        new Claim(ClaimTypes.Role, user.Role.ToString()),
        new Claim("tenant_id", user.TenantId.ToString())
    };

    // Stamp every permission for this role as an individual claim
    foreach (var permission in RolePermissions.For(user.Role))
    {
        claims.Add(new Claim("permission", permission.ToString()));
    }

    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(claims),
        Expires = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationInMinutes),
        Issuer = _jwtOptions.Issuer,
        Audience = _jwtOptions.Audience,
        SigningCredentials = new SigningCredentials(
            new SymmetricSecurityKey(key),
            SecurityAlgorithms.HmacSha256Signature)
    };

    var tokenHandler = new JwtSecurityTokenHandler();
    var token = tokenHandler.CreateToken(tokenDescriptor);
    return tokenHandler.WriteToken(token);
}
```

After this change an Admin JWT contains claims like:

```
"permission": "StudentsCreate"
"permission": "StudentsRead"
"permission": "StaffApproveLeave"
... (all Admin permissions)
```

A Teacher JWT contains only Teacher permissions. The token is self-contained.

---

## Step 3: API Layer — Authorization Plumbing

Create a folder `src/Api/Authorization/` and add three files.

### HasPermissionRequirement

A named marker that says "this endpoint needs permission X."

```csharp
// src/Api/Authorization/HasPermissionRequirement.cs
using Microsoft.AspNetCore.Authorization;
using SchoolMaster.Domain.Enums;

namespace SchoolMaster.Api.Authorization;

public class HasPermissionRequirement : IAuthorizationRequirement
{
    public Permission Permission { get; }

    public HasPermissionRequirement(Permission permission)
    {
        Permission = permission;
    }
}
```

### HasPermissionHandler

Reads the `permission` claims from the JWT and checks whether the required permission is present. No database call. No service injection.

```csharp
// src/Api/Authorization/HasPermissionHandler.cs
using Microsoft.AspNetCore.Authorization;
using SchoolMaster.Domain.Enums;

namespace SchoolMaster.Api.Authorization;

public class HasPermissionHandler : AuthorizationHandler<HasPermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        HasPermissionRequirement requirement)
    {
        var permissionClaims = context.User.Claims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .ToHashSet();

        if (permissionClaims.Contains(requirement.Permission.ToString()))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
```

`ToHashSet()` makes the lookup O(1) rather than O(n). Small detail, correct behaviour.

### HasPermissionAttribute

A thin wrapper so controller actions read as `[HasPermission(Permission.X)]` rather than `[Authorize(Policy = "StudentsCreate")]`.

```csharp
// src/Api/Authorization/HasPermissionAttribute.cs
using Microsoft.AspNetCore.Authorization;
using SchoolMaster.Domain.Enums;

namespace SchoolMaster.Api.Authorization;

public class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(Permission permission)
        : base(permission.ToString())
    {
    }
}
```

### Register Policies in Program.cs

Replace `builder.Services.AddAuthorization()` with:

```csharp
// src/Api/Program.cs

using SchoolMaster.Api.Authorization;
using SchoolMaster.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

builder.Services.AddAuthorization(options =>
{
    foreach (var permission in Enum.GetValues<Permission>())
    {
        options.AddPolicy(permission.ToString(), policy =>
            policy.Requirements.Add(new HasPermissionRequirement(permission)));
    }
});

builder.Services.AddScoped<IAuthorizationHandler, HasPermissionHandler>();
```

The `foreach` loop registers one policy per permission automatically. When you add a new permission to the enum, the policy exists without touching `Program.cs` again.

---

## Step 4: Controllers — Usage

**Before:**
```csharp
[Authorize(Roles = "Admin")]
[HttpPost]
public async Task<IActionResult> EnrollStudent([FromBody] EnrollStudentRequest request) { ... }
```

**After:**
```csharp
[HasPermission(Permission.StudentsCreate)]
[HttpPost]
public async Task<IActionResult> EnrollStudent([FromBody] EnrollStudentRequest request) { ... }
```

---

## What to Watch Out For

**Bare `[Authorize]` still works but skips permission checks.** A bare `[Authorize]` only checks "is the user authenticated?" Make sure every protected endpoint uses `[HasPermission(...)]`, not a bare `[Authorize]`.

**Stale tokens after deployment.** Users logged in before you deploy this change have JWTs with no `permission` claims. They will get `403` on protected endpoints until they log in again and receive a fresh token. Note this in your deploy runbook.

**Permission vs. ownership are two separate concerns.** `Parent` gets `StudentsRead` — that grants access to `GET /students/{id}`. The service layer still enforces that a Parent may only retrieve their own child. Never put ownership logic in the authorization handler.

**Do not inject `IUserRepository` or any service into `HasPermissionHandler`.** If you find yourself needing a database call inside the handler, that logic belongs in the service. The handler reads claims only.

**TenantId boundary is unchanged.** The permission system controls which endpoints a user can reach. TenantId scoping (via the EF Core global query filter) controls which rows they can see. Both must be in place.

---

## What to Test

### Unit Tests on RolePermissions

- `Admin_HasStudentsCreatePermission`
- `Teacher_DoesNotHaveStudentsCreatePermission`
- `Parent_HasStudentsReadButNotStudentsCreate`
- `Staff_HasOnlyStaffReadAndSubmitLeave`
- `Student_HasOnlyAcademicViewTimetable`
- `ForUnknownRole_ReturnsEmptyList`

### Unit Tests on HasPermissionHandler

- `Handler_Succeeds_WhenPermissionClaimIsPresent`
- `Handler_DoesNotSucceed_WhenPermissionClaimIsMissing`
- `Handler_DoesNotSucceed_WhenUserHasNoPermissionClaims`
- `Handler_DoesNotSucceed_WhenPermissionClaimHasDifferentValue`

### Integration Tests

- `POST /students` returns `403` with a Teacher JWT
- `POST /students` returns `201` with an Admin JWT
- `POST /attendance` returns `403` with an Admin JWT
- `POST /attendance` returns `200` with a Teacher JWT
- `GET /students/{id}` returns `200` for Admin and Teacher JWTs
- Any protected endpoint returns `401` with no JWT at all

---

## Implementation Order

1. Add `Staff` to `UserRole` enum
2. Create `Permission` enum in `src/Domain/Enums/`
3. Create `RolePermissions` in `src/Domain/Authorization/`
4. Update `JwtService.GenerateAccessToken` to stamp permission claims
5. Create `HasPermissionRequirement`, `HasPermissionHandler`, `HasPermissionAttribute` in `src/Api/Authorization/`
6. Update `Program.cs` to register policies and `HasPermissionHandler`
7. Replace all `[Authorize(Roles = "...")]` usages across all controllers
8. Write unit and integration tests
