namespace SchoolMaster.Domain.Entities;

public class Guardian
{
    public required Guid Id { get; set; }
    public required Guid UserId { get; set; }
    public required Guid TenantId { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Phone { get; set; }
    public required string Email { get; set; }
    
    public User? User { get; set; }
    public ICollection<Student> Students { get; set; } = new List<Student>();
}
