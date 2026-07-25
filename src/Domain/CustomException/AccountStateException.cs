namespace SchoolMaster.Domain.CustomException;

// Thrown at login when credentials are valid but the account is not permitted to sign in.
public class EmailNotVerifiedException(string message) : Exception(message);
public class AccountInactiveException(string message) : Exception(message);
