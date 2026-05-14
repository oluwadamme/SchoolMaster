namespace SchoolMaster.Domain.CustomException;

public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException(string message) : base(message) { }
}