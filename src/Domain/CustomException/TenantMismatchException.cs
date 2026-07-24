namespace SchoolMaster.Domain.CustomException;

// Thrown when an authenticated request carries an X-Tenant-Subdomain header that resolves to a
// different tenant than the one in the caller's JWT. Signals an attempt to act across tenants.
public class TenantMismatchException(string message) : Exception(message);

// Thrown at login when an account is temporarily locked after too many failed attempts.
public class AccountLockedException(string message) : Exception(message);
