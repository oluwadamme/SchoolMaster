namespace SchoolMaster.Application.DTOs;
public record BulkEnrollmentResult(
    int TotalRows,
    int Successful,
    int Failed,
    IReadOnlyList<BulkEnrollmentFailure> Failures);

public record BulkEnrollmentFailure(int Row, string Email, string Reason);
