namespace SchoolMaster.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Infrastructure.Persistence;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;

public class UserRepository : IUserRepository
{
    private readonly SchoolMasterContext _context;

    public UserRepository(SchoolMasterContext context)
    {
        _context = context;
    }

    public async Task AddUserAsync(User user)
    {
        await _context.Users.AddAsync(user);
        // await _context.SaveChangesAsync();
    }

    public async Task AddUsersBulkAsync(IEnumerable<User> users)
    {
        await _context.Users.AddRangeAsync(users);
    }

    public async Task<HashSet<string>> GetExistingEmailsAsync(IEnumerable<string> emails, Guid tenantId)
    {
        var emailList = emails.ToList();
        var existing = await _context.Users
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && emailList.Contains(x.Email))
            .Select(x => x.Email)
            .ToListAsync();
        // Hashset Organizes items in a list such that you can instantly jump straight to an item
        // instead of looking one by one
        // "Take the emails we got from the database, put them into a super-fast box, and make sure capital letters do not matter
        return new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> ExistsByEmailInTenantAsync(string email, Guid tenantId)
    {
        // IgnoreQueryFilters: onboarding runs before the new tenant is the ambient tenant, so the
        // global filter cannot scope this. The explicit tenantId gives the per-tenant guarantee.
        // Email is unique per tenant (see the (TenantId, Email) index), not globally.
        return await _context.Users
            .IgnoreQueryFilters()
            .AnyAsync(x => x.Email == email && x.TenantId == tenantId);
    }

    public async Task<bool> ExistsByEmailAndTenantIdAsync(string email, Guid tenantId)
    {
        return await _context.Users.IgnoreQueryFilters()
            .AnyAsync(x => x.Email == email && x.TenantId == tenantId);
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        // Tenant scoping comes from the global query filter (_currentTenant.Id). Status is
        // intentionally NOT filtered here: email-verification and password-reset flows operate on
        // non-active users. The login flow enforces account status itself.
        return await _context.Users
            .FirstOrDefaultAsync(x => x.Email == email);
    }

    public async Task<User?> GetUserByEmailAndTenantIdAsync(string email, Guid tenantId)
    {
        // Tenant scoping comes from the global query filter (_currentTenant.Id); the explicit
        // tenantId parameter is retained for the existing staff-invitation call sites.
        return await _context.Users
            .FirstOrDefaultAsync(x => x.Email == email);
    }

    public async Task<User?> GetUserByIdAsync(Guid userId, Guid tenantId)
    {
        // IgnoreQueryFilters: the refresh-token endpoint has no [Authorize] attribute, so
        // httpContext.User carries no tenant_id claim and the global filter would return Guid.Empty,
        // blocking the lookup. The explicit tenantId parameter provides the isolation guarantee.
        return await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == userId && x.TenantId == tenantId && x.Status == UserStatus.Active);
    }


    public Task UpdateUserAsync(User user)
    {
        _context.Users.Update(user);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

}
