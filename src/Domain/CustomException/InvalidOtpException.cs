namespace SchoolMaster.Domain.CustomException;
public class InvalidOtpException : Exception
{
    public InvalidOtpException(string message) : base(message) { }
}