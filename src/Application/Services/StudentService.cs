using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Domain.CustomException;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
using System.Transactions;

namespace SchoolMaster.Application.Services;

public class StudentService : IStudentService
{
    private readonly IUserRepository _userRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICurrentTenant _currentTenant;

    public StudentService(
        IUserRepository userRepository, 
        IStudentRepository studentRepository, 
        ITenantRepository tenantRepository,
        ICurrentTenant currentTenant)
    {
        _userRepository = userRepository;
        _studentRepository = studentRepository;
        _tenantRepository = tenantRepository;
        _currentTenant = currentTenant;
    }

    public async Task<BaseResponse<StudentResponse>> CreateStudentAsync(CreateStudentRequest request)
    {
        var tenantId = _currentTenant.Id;
        if (tenantId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Tenant context is required.");
        }

        // 1. Business Rule: Ensure unique identity and student number within the school
        if (await _userRepository.ExistsByEmailAndTenantIdAsync(request.Email, tenantId))
        {
            throw new AlreadyExistException($"Email {request.Email} is already registered.");
        }

        // Generate Permanent ID: GHA/2024/0001
        var studentNumber = await GeneratePermanentStudentNumber(tenantId);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Roles = new List<UserRole> { UserRole.Student },
            Status = UserStatus.Active,
            IsEmailVerified = false,
            CreatedAt = DateTime.UtcNow
        };
        await _userRepository.AddUserAsync(user);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = tenantId,
            StudentNumber = studentNumber,
            FirstName = request.FirstName,
            LastName = request.LastName,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            GuardianName = request.GuardianName,
            GuardianPhone = request.GuardianPhone,
            GuardianEmail = request.GuardianEmail,
            MedicalNotes = request.MedicalNotes,
            PhotoUrl = request.PhotoUrl,
            Status = StudentStatus.Active,
            EnrolledAt = DateTime.UtcNow
        };
        await _studentRepository.AddStudentAsync(student);

        scope.Complete();

        var response = new StudentResponse(
            student.Id, 
            student.TenantId, 
            student.FirstName, 
            student.LastName, 
            student.StudentNumber, 
            student.DateOfBirth, 
            student.Gender, 
            student.GuardianName, 
            student.GuardianPhone, 
            student.GuardianEmail,  
            student.PhotoUrl);
        
        return BaseResponse<StudentResponse>.SuccessResponse("Student enrolled successfully.", response);
    }

    private async Task<string> GeneratePermanentStudentNumber(Guid tenantId)
    {
        // 1. Get the school's code (e.g., GHA)
        var tenant = await _tenantRepository.GetByIdAsync(tenantId);
        if (tenant == null)
        {
            throw new TenantNotFoundException("School identification not found.");
        }

        var year = DateTime.UtcNow.Year;
        var prefix = $"{tenant.SchoolCode}/{year}/";

        // 2. Find the last assigned code for this school and year
        var lastCode = await _studentRepository.GetLastStudentNumberAsync(tenantId, prefix);

        int nextSequence = 1;
        if (lastCode != null)
        {
            // 3. Extract sequence from "GHA/2024/000015" and increment
            var parts = lastCode.Split('/');
            if (parts.Length == 3 && int.TryParse(parts[2], out int lastSeq))
            {
                nextSequence = lastSeq + 1;
            }
        }

        // 4. Return formatted ID with 6-digit padding (000001, 000002...)
        return $"{prefix}{nextSequence:D6}";
    }
}