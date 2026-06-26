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

    public async Task<bool> ExistsByEmailAsync(string email)
    {
        return await _context.Users.IgnoreQueryFilters().AnyAsync(x => x.Email == email);
    }

    public async Task<bool> ExistsByEmailAndTenantIdAsync(string email, Guid tenantId)
    {
        return await _context.Users.IgnoreQueryFilters()
            .AnyAsync(x => x.Email == email && x.TenantId == tenantId);
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _context.Users
            .FirstOrDefaultAsync(x => x.Email == email && x.Status == UserStatus.Active);
    }

    public async Task<User?> GetUserByEmailAndTenantIdAsync(string email, Guid tenantId)
    {
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
