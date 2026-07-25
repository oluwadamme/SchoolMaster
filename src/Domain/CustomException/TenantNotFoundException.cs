namespace SchoolMaster.Domain.CustomException;

public class TenantNotFoundException : Exception
{
    public TenantNotFoundException(string message) : base(message) { }
}
