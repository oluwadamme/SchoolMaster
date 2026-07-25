namespace SchoolMaster.Domain.Entities;
public class TenantNumberSequence
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string SequenceType { get; private set; }  // "STAFF" or "STUDENT"
    public int Year { get; private set; }
    public long LastValue { get; private set; }
}
