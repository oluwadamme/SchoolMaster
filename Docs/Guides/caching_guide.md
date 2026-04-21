# Caching & Redis: A Beginner's Guide

Imagine your `SchoolMaster` becomes wildly successful. Users are constantly hitting `GET /api/v1/students` to see the list of enrolled students.

Every single time that endpoint is hit, Entity Framework executes a SQL query, contacts the PostgreSQL database over the network, waits for the database to read from its hard drive, and sends the data back. **This is slow and expensive.**

If the list of students hasn't changed in the last 10 minutes, why keep asking the database for it?

**Caching** is the act of temporarily saving that data in the server's extremely fast RAM.

---

## 1. Local Caching with `IMemoryCache`

The simplest form of caching in .NET is `IMemoryCache`. It stores your data directly in the RAM of the server (or Docker container) running your application.

### How it works (Conceptually)
1. User requests students.
2. The API checks the Cache (RAM) to see if the student list is there.
3. If they aren't, the API queries the Database.
4. The API saves the database result into the Cache with an expiration timer (e.g., "keep this for 5 minutes").
5. The API returns the students to the user.

For the next 5 minutes, if *anyone* asks for the students, The API snatches them instantly from RAM without ever touching the database!

### Code Example (`StudentService.cs`):
```csharp
using Microsoft.Extensions.Caching.Memory;

public class StudentService : IStudentService
{
    private readonly SchoolMasterContext _db;
    private readonly IMemoryCache _cache;

    public StudentService(SchoolMasterContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<List<StudentDto>> GetStudentsAsync()
    {
        // 1. Check if "AllStudents" exists in the cache
        if (!_cache.TryGetValue("AllStudents", out List<StudentDto> students))
        {
            // 2. If not, fetch from DB
            students = await _db.Students
                .ProjectTo<StudentDto>(_mapper.ConfigurationProvider)
                .ToListAsync();

            // 3. Save it to cache for 5 minutes
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
            
            _cache.Set("AllStudents", students, cacheOptions);
        }

        return students;
    }
}
```

---

## 2. The Multi-Instance Problem

`IMemoryCache` is incredibly fast, but it has a fatal flaw when you scale up.

If your app gets so much traffic that you decide to run **3 Docker containers** (Server A, Server B, and Server C) behind a Load Balancer, `IMemoryCache` breaks synchronization.

1. User 1 hits Server A. Server A fetches the students, and caches them in **Server A's RAM**.
2. An Admin hits Server B, and enrolls a *new student*.
3. User 2 hits Server A. Server A returns the cached list... but the cache is stale! It doesn't have the new student!
4. User 3 hits Server C. Server C's RAM is empty, so it hits the database and gets the updated list.

Now you have a broken API where users see different data depending on which container the load balancer routes them to.

---

## 3. Distributed Caching with Redis

**Redis** solves the multi-instance problem. 

Redis is an external, deeply optimized, lightning-fast database that runs entirely in RAM. Instead of Server A, B, and C saving the cache to their own local memory, they all agree to save and read the cache from a single, centralized Redis server.

### How it works:
If you add Redis to your app, you use `IDistributedCache` instead of `IMemoryCache`. 

1. Server A receives a request, checks the Redis server, and if it's empty, asks PostgreSQL.
2. Server A saves the result into Redis.
3. User 2 hits Server C. Server C checks Redis, sees the data is already there, and returns it instantly!

If Server B enrolls or withdraws a student, it tells Redis to wipe the cache. The next request (regardless of which server handles it) will fetch fresh data from the database.

---

### In Summary
- Use **`IMemoryCache`** if you are running a single server/container. It is extremely easy to set up and requires zero external infrastructure.
- Use **Redis (`IDistributedCache`)** when you scale up to multiple running instances of your API to guarantee perfectly synchronized Cache data across your entire cluster.
