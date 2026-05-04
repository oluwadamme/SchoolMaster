using Microsoft.EntityFrameworkCore;

public class UserRepository : IUserRepository
{
    private readonly SchoolMasterContext _context;

    public UserRepository(SchoolMasterContext context)
    {
        _context = context;
    }

    public async Task AddAsync(User user)
    {
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsByEmailAsync(string email)
    {
        return await _context.Users.AnyAsync(x => x.Email == email);
    }
}
