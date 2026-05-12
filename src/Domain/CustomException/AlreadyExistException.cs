namespace SchoolMaster.Domain.CustomException;

public class AlreadyExistException : Exception
{
    public AlreadyExistException(string message) : base(message) { }
}