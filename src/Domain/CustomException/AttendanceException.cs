namespace SchoolMaster.Domain.CustomException;

public class DuplicateAttendanceException(string message) : Exception(message);
public class StudentNotInClassException(string message) : Exception(message);
