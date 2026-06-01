// src/Domain/CustomException/AcademicYearNotFoundException.cs
namespace SchoolMaster.Domain.CustomException;
public class AcademicYearNotFoundException(string message) : Exception(message);


public class ClassNotFoundException(string message) : Exception(message);


public class SubjectNotFoundException(string message) : Exception(message);


public class DuplicateAcademicYearException(string message) : Exception(message);


public class DuplicateClassNameException(string message) : Exception(message);
