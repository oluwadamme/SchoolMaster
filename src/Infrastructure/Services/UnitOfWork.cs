using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Infrastructure.Persistence;

namespace SchoolMaster.Infrastructure.Services;
public class UnitOfWork : IUnitOfWork
{
    private readonly SchoolMasterContext _context;
    public UnitOfWork(SchoolMasterContext context) => _context = context;
    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}