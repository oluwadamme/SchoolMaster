namespace SchoolMaster.Domain.CustomException;

public class OtpExpiredException : Exception
{
    public OtpExpiredException(string message) : base(message) { }
}