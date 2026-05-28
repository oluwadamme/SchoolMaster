namespace SchoolMaster.Application.Services.Interfaces;
public interface IUnitOfWork
{
    Task SaveChangesAsync();
}