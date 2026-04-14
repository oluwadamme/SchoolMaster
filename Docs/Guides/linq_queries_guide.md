# Explicit LINQ Queries in EF Core: A Beginner's Guide

Right now, your repository contains perfectly fine LINQ queries, but they are what we call "Implicit" or "Basic" queries. 

For example, look at this query:
```csharp
// Implicit Query
public async Task<List<Student>> GetAllStudentsAsync(Guid tenantId)
{
    return await context.Students.Where(b => b.TenantId == tenantId).ToListAsync();
}
```

Behind the scenes, Entity Framework (EF) Core turns this C# code into a SQL query that looks like this:
```sql
SELECT "Id", "FirstName", "LastName", "DateOfBirth", "Gender", "ClassId", "TenantId" ...
FROM "Students" 
WHERE "TenantId" = @tenantId;
```

This is known as a `SELECT *` query. It pulls **every single column** of the database table into your server's RAM.

If you add a massive column to your `Students` table later (like a binary blob for a high-res photo or medical history text), this implicit query will blindly pull all that data into RAM for *every single student* down the wire, causing a massive memory bottleneck!

---

## 1. Explicit Filtering with `.Select()`

Writing "Explicit" LINQ chains gives you surgical control over exactly what EF Core asks the database to do. 

If your `GetAllStudentsAsync` method only needs to return students for a lightweight list on a dashboard (where the user only sees the First Name and Last Name), you should use `.Select()` to explicitly map it to a DTO directly inside the database query.

```csharp
public async Task<List<StudentSummaryDto>> GetAllStudentsAsync(Guid tenantId)
{
    return await context.Students
        .Where(b => b.TenantId == tenantId)
        // Explicitly map exactly the columns we want BEFORE hitting the database!
        .Select(b => new StudentSummaryDto 
        {
            Id = b.Id,
            FirstName = b.FirstName,
            LastName = b.LastName,
            ClassName = b.Class.Name
        })
        .OrderBy(b => b.LastName) // Explicitly sort the data on the database side
        .ToListAsync();
}
```

By chaining the `.Select()` method, EF Core writes a highly optimized SQL query:
```sql
SELECT s."Id", s."FirstName", s."LastName", c."Name" 
FROM "Students" AS s
JOIN "Classes" AS c ON s."ClassId" = c."Id"
WHERE s."TenantId" = @tenantId
ORDER BY s."LastName";
```
It completely ignores the massive photo columns and medical notes, saving massive amounts of bandwidth and RAM!

---

## 2. Explicit Optimization with `.AsNoTracking()`

Another massive part of explicitly optimizing LINQ chains is using **Tracking**.

Normally, whenever you query a `Student`, EF Core creates a secret "tracker" in RAM. It watches the Student object to see if you make any changes to it so that if you call `await context.SaveChangesAsync()`, it knows exactly what to UPDATE in the database.

However, for your `GetAllStudentsAsync` method, the user is just reading the students. You are never going to edit them and save them. 

You can explicitly tell EF Core to stop tracking the objects by chaining `.AsNoTracking()`. This makes read queries up to **300% faster** and uses significantly less memory!

### The Ultimate Explicit Query

```csharp
public async Task<List<Student>> GetAllStudentsAsync(Guid tenantId)
{
    return await context.Students
        .AsNoTracking()                  // 1. Never track this (huge speed boost for reads!)
        .Where(b => b.TenantId == tenantId) // 2. Filter the rows
        .OrderBy(b => b.LastName)        // 3. Sort the rows
        .ToListAsync();                  // 4. Execute the SQL and return the list
}
```

---

## In Summary

Basic implicit queries like `context.Students.FirstOrDefaultAsync(...)` are great for rapid prototyping.

However, as your application grows, replacing them with explicit Method Chains (`.AsNoTracking().Where(...).Select(...).OrderBy(...)`) gives you granular control over SQL translation. It forces you to think about exactly what data is crossing the wire between PostgreSQL and your C# application, resulting in an incredibly fast API.
