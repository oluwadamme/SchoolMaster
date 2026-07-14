using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Domain.CustomException;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
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

    public async Task<BaseResponse<IReadOnlyList<StudentResponse>>> GetAllStudentsAsync()
    {
        var tenantId = _currentTenant.Id;
        
        var studentList = await _studentRepository.GetAllStudentsAsync(tenantId);
        
        var responseList = studentList.Select(student => new StudentResponse(
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
            student.PhotoUrl
        )).ToList();

        return BaseResponse<IReadOnlyList<StudentResponse>>.SuccessResponse("Students retrieved successfully.", responseList);
    }

    // Update student and associated user
    public async Task<BaseResponse<StudentResponse>> UpdateStudentAsync(UpdateStudentRequest request)
    {
        var tenantId = _currentTenant.Id;

        // Fetch existing student (ignoring global filters for safety)
        var student = await _studentRepository.GetStudentByIdIgnoringFiltersAsync(request.StudentId, tenantId);
        if (student == null)
        {
            throw new UserNotFoundException("Student not found.");
        }

        // Fetch associated user
        var user = await _userRepository.GetUserByIdAsync(student.UserId, tenantId);
        if (user == null)
        {
            throw new UserNotFoundException("Associated user not found.");
        }

        // Update mutable fields if provided
        if (request.FirstName.HasValue && request.FirstName.Value != null) { student.FirstName = request.FirstName.Value; user.FirstName = request.FirstName.Value; }
        if (request.LastName.HasValue && request.LastName.Value != null) { student.LastName = request.LastName.Value; user.LastName = request.LastName.Value; }
        if (request.Email.HasValue && request.Email.Value != null) { user.Email = request.Email.Value; }
        if (request.DateOfBirth.HasValue) student.DateOfBirth = request.DateOfBirth.Value;
        if (request.Gender.HasValue) student.Gender = request.Gender.Value;
        if (request.GuardianName.HasValue && request.GuardianName.Value != null) student.GuardianName = request.GuardianName.Value;
        if (request.GuardianPhone.HasValue && request.GuardianPhone.Value != null) student.GuardianPhone = request.GuardianPhone.Value;
        if (request.GuardianEmail.HasValue && request.GuardianEmail.Value != null) student.GuardianEmail = request.GuardianEmail.Value;
        if (request.MedicalNotes.HasValue && request.MedicalNotes.Value != null) student.MedicalNotes = request.MedicalNotes.Value;
        if (request.PhotoUrl.HasValue && request.PhotoUrl.Value != null) student.PhotoUrl = request.PhotoUrl.Value;

        // Persist changes
        await _userRepository.UpdateUserAsync(user);

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

        return BaseResponse<StudentResponse>.SuccessResponse("Student updated successfully.", response);
    }

    public async Task<BaseResponse<StudentResponse>> CreateStudentAsync(CreateStudentRequest request)
    {
        var tenantId = _currentTenant.Id;
        // 1. Business Rule: Ensure unique identity and student number within the school
        if (await _userRepository.ExistsByEmailAndTenantIdAsync(request.Email, tenantId))
        {
            throw new AlreadyExistException($"Email {request.Email} is already registered.");
        }

        (User user, Student student) = await CreateStudentAndUserObject(request, tenantId);
        await _studentRepository.AddStudentAsync(student);
        await _userRepository.AddUserAsync(user);


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
            student.PhotoUrl
        );

        return BaseResponse<StudentResponse>.SuccessResponse("Student enrolled successfully.", response);
    }

    private async Task<(User user, Student student)> CreateStudentAndUserObject(CreateStudentRequest request, Guid tenantId)
    {
        // Generate Permanent ID: GHA/2024/0001
        var studentNumber = await GeneratePermanentStudentNumber(tenantId);

        var user = User.Create(
            tenantId: tenantId,
            status: UserStatus.Active,
            roles: new List<UserRole> { UserRole.Student },
            firstName: request.FirstName,
            lastName: request.LastName,
            email: request.Email,
            passwordHash: BCrypt.Net.BCrypt.HashPassword(request.Password)
        );

        var student = new Student
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = tenantId,
            ClassId = request.ClassId,
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
        return (user, student);
    }

    // ✅ Bulk enrollment for students with partial success
    //  the code takes all 1,000 students and puts them into one big group in the computer's memory.
    // Then, it connects to the database exactly one time
    public async Task<BaseResponse<IReadOnlyList<StudentResponse>>> EnrollStudentsBulkAsync(BulkEnrollStudentsRequest requests)
    {
        var tenantId = _currentTenant.Id;

        var requestList = requests.Students.ToList();
        if (!requestList.Any())
            return BaseResponse<IReadOnlyList<StudentResponse>>.SuccessResponse("No students to enroll.", new List<StudentResponse>());

        // 1. Make a list of request emails and fetch all emails that already exist in the database
        var emails = requestList.Select(x => x.Email).Distinct().ToList();
        var existingEmails = await _userRepository.GetExistingEmailsAsync(emails, tenantId);

        // 2. Fetch sequence for student number
        var tenant = await _tenantRepository.GetByIdAsync(tenantId);
        if (tenant == null) throw new TenantNotFoundException("School identification not found.");

        var year = DateTime.UtcNow.Year;
        var prefix = $"{tenant.SchoolCode}/{year}/";
        var lastCode = await _studentRepository.GetLastStudentNumberAsync(tenantId, prefix);

        // logic for assigning the next student number
        int nextSequence = 1;
        if (lastCode != null)
        {
            var parts = lastCode.Split('/');
            if (parts.Length == 3 && int.TryParse(parts[2], out int lastSeq))
            {
                nextSequence = lastSeq + 1;
            }
        }

        var results = new List<StudentResponse>();
        var usersToInsert = new List<User>();
        var studentsToInsert = new List<Student>();

        // incase there are duplicate emails in the request list itself, we create an empty box
        // that checks and adds each email as they are processed and if there's a duplicate
        // the error is recorded
        var processedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var request in requestList)
        {
            if (existingEmails.Contains(request.Email) || processedEmails.Contains(request.Email))
            {
                results.Add(new StudentResponse(
                    Guid.Empty,
                    tenantId,
                    request.FirstName,
                    request.LastName,
                    "N/A",
                    DateOnly.MinValue,
                    request.Gender,
                    request.GuardianName,
                    request.GuardianPhone,
                    request.GuardianEmail,
                    request.PhotoUrl
                ));
                continue;
            }

            var studentNumber = $"{prefix}{nextSequence:D6}";
            nextSequence++;
            var (user, student) = await CreateStudentAndUserObject(request, tenantId); // Create student in bulk mode

            usersToInsert.Add(user);
            studentsToInsert.Add(student);
            processedEmails.Add(request.Email);

            var studentResponse = new StudentResponse(
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
                student.PhotoUrl
                );
            results.Add(studentResponse);
        }

        if (usersToInsert.Any())
        {
            await _userRepository.AddUsersBulkAsync(usersToInsert);
            await _studentRepository.AddStudentsBulkAsync(studentsToInsert);
        }

        return BaseResponse<IReadOnlyList<StudentResponse>>.SuccessResponse(
            "Bulk student enrollment completed.", results);
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