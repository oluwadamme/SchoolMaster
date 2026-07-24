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
    private readonly IGuardianRepository _guardianRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantSequenceRepository _tenantSequenceRepository;
    private readonly ICurrentTenant _currentTenant;

    public StudentService(
        IUserRepository userRepository, 
        IStudentRepository studentRepository, 
        IGuardianRepository guardianRepository,
        ITenantRepository tenantRepository,
        ITenantSequenceRepository tenantSequenceRepository,
        ICurrentTenant currentTenant)
    {
        _userRepository = userRepository;
        _studentRepository = studentRepository;
        _guardianRepository = guardianRepository;
        _tenantRepository = tenantRepository;
        _tenantSequenceRepository = tenantSequenceRepository;
        _currentTenant = currentTenant;
    }

    public async Task<BaseResponse<PagedResponse<StudentResponse>>> GetAllStudentsAsync(int page, int pageSize)
    {
        var tenantId = _currentTenant.Id;
        
        var (studentList, totalCount) = await _studentRepository.GetAllStudentsAsync(tenantId, page, pageSize);
        
        var responseList = studentList.Select(student => new StudentResponse(
            student.Id,
            student.TenantId,
            student.FirstName,
            student.LastName,
            student.StudentNumber,
            student.DateOfBirth,
            student.Gender,
            student.Guardian?.FirstName ?? "",
            student.Guardian?.LastName ?? "",
            student.Guardian?.Phone ?? "",
            student.Guardian?.Email ?? "",
            student.PhotoUrl
        )).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var pagedResponse = new PagedResponse<StudentResponse>(responseList, totalCount, totalPages, page, pageSize);
        return BaseResponse<PagedResponse<StudentResponse>>.SuccessResponse("Students retrieved successfully.", pagedResponse);
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
        if (request.GuardianFirstName.HasValue && request.GuardianFirstName.Value != null) student.Guardian.FirstName = request.GuardianFirstName.Value;
        if (request.GuardianLastName.HasValue && request.GuardianLastName.Value != null) student.Guardian.LastName = request.GuardianLastName.Value;
        if (request.GuardianPhone.HasValue && request.GuardianPhone.Value != null) student.Guardian.Phone = request.GuardianPhone.Value;
        if (request.GuardianEmail.HasValue && request.GuardianEmail.Value != null) student.Guardian.Email = request.GuardianEmail.Value;
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
            student.Guardian?.FirstName ?? "",
            student.Guardian?.LastName ?? "",
            student.Guardian?.Phone ?? "",
            student.Guardian?.Email ?? "",
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

        var (user, student, newGuardian, newGuardianUser) = await CreateStudentGuardianAndUserObject(request, tenantId);
        
        if (newGuardianUser != null) await _userRepository.AddUserAsync(newGuardianUser);
        if (newGuardian != null) await _guardianRepository.AddGuardianAsync(newGuardian);
        
        await _userRepository.AddUserAsync(user);
        await _studentRepository.AddStudentAsync(student);


        var response = new StudentResponse(
            student.Id,
            student.TenantId,
            student.FirstName,
            student.LastName,
            student.StudentNumber,
            student.DateOfBirth,
            student.Gender,
            student.Guardian?.FirstName ?? "",
            student.Guardian?.LastName ?? "",
            student.Guardian?.Phone ?? "",
            student.Guardian?.Email ?? "",
            student.PhotoUrl
        );

        return BaseResponse<StudentResponse>.SuccessResponse("Student enrolled successfully.", response);
    }

    private async Task<(User user, Student student, Guardian? newGuardian, User? newGuardianUser)> CreateStudentGuardianAndUserObject(
        CreateStudentRequest request, Guid tenantId, string? preGeneratedStudentNumber = null, Dictionary<string, Guardian>? guardianCache = null)
    {
        Guardian? guardianToLink = null;
        Guardian? newGuardian = null;
        User? newGuardianUser = null;
        
        var normalizedGuardianEmail = request.GuardianEmail.Trim().ToLowerInvariant();

        if (guardianCache != null && guardianCache.TryGetValue(normalizedGuardianEmail, out var cachedG))
        {
            guardianToLink = cachedG;
        }
        else if (guardianCache == null)
        {
            // 1. Check if guardian exists by email
            guardianToLink = await _guardianRepository.GetGuardianByEmailAsync(request.GuardianEmail, tenantId);
        }

        if (guardianToLink == null)
        {
            // Create a User for the new Guardian
            newGuardianUser = User.Create(
                tenantId: tenantId,
                status: UserStatus.Active,
                roles: new List<UserRole> { UserRole.Parent },
                firstName: request.GuardianFirstName,
                lastName: request.GuardianLastName,
                email: request.GuardianEmail,
                passwordHash: BCrypt.Net.BCrypt.HashPassword("DefaultPassword123!") // Needs to be generated or handled better
            );

            newGuardian = new Guardian
            {
                Id = Guid.NewGuid(),
                UserId = newGuardianUser.Id,
                TenantId = tenantId,
                FirstName = request.GuardianFirstName,
                LastName = request.GuardianLastName,
                Phone = request.GuardianPhone,
                Email = request.GuardianEmail
            };
            guardianToLink = newGuardian;
            
            if (guardianCache != null)
            {
                guardianCache[normalizedGuardianEmail] = newGuardian;
            }
        }

        // Generate Permanent ID: GHA/2024/0001
        var studentNumber = preGeneratedStudentNumber ?? await GeneratePermanentStudentNumber(tenantId);

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
            GuardianId = guardianToLink.Id,
            Guardian = guardianToLink, // Link it here
            MedicalNotes = request.MedicalNotes,
            PhotoUrl = request.PhotoUrl,
            Status = StudentStatus.Active,
            EnrolledAt = DateTime.UtcNow
        };
        
        return (user, student, newGuardian, newGuardianUser);
    }

    // ✅ Bulk enrollment for students with partial success
    //  the code takes all 1,000 students and puts them into one big group in the computer's memory.
    // Then, it connects to the database exactly one time
    public async Task<BaseResponse<BulkEnrollmentResult>> EnrollStudentsBulkAsync(BulkEnrollStudentsRequest requests)
    {
        var tenantId = _currentTenant.Id;
        var requestList = requests.Students.ToList();
        var tenant = await _tenantRepository.GetByIdAsync(tenantId);
        if (tenant == null) throw new TenantNotFoundException("School identification not found.");

        // 1. Make a list of request emails and fetch all emails that already exist in the database
        var studentEmails = requestList.Select(x => x.Email.Trim().ToLowerInvariant()).Distinct().ToList();
        var existingStudentEmails = await _userRepository.GetExistingEmailsAsync(studentEmails, tenantId);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var accepted = new List<(int Row, BulkEnrollStudentItemRequest Req)>();
        var failures = new List<BulkEnrollmentFailure>();

        for (var i = 0; i < requestList.Count; i++)
        {
            var email = requestList[i].Email.Trim().ToLowerInvariant();
            if (existingStudentEmails.Contains(email) || !seen.Add(email))
            {
                failures.Add(new BulkEnrollmentFailure(i + 1, requestList[i].Email, "Email already exists."));
                continue;
            }
            accepted.Add((i + 1, requestList[i]));
        }

        if (accepted.Count > 0)
        {
            var usersToInsert = new List<User>();
            var studentsToInsert = new List<Student>();
            var guardiansToInsert = new List<Guardian>();
            var guardianUsersToInsert = new List<User>();

            // Fetch existing guardians by email for the accepted students
            var guardianEmails = accepted.Select(x => x.Req.GuardianEmail.Trim().ToLowerInvariant()).Distinct().ToList();
            var existingGuardiansList = await _guardianRepository.GetGuardiansByEmailsAsync(guardianEmails, tenantId);
            // turns into dictionary using g.email as the key
            var guardianCache = existingGuardiansList.ToDictionary(g => g.Email.ToLowerInvariant());

            // Bulk generate student numbers
            var year = DateTime.UtcNow.Year;
            var prefix = $"{tenant.SchoolCode}/{year}/";
            var startingSequence = await _tenantSequenceRepository.ReserveBlockAsync(tenantId, "STUDENT", year, accepted.Count);

            for (var i = 0; i < accepted.Count; i++)
            {
                var req = accepted[i].Req;
                var currentSequence = startingSequence + i;
                var studentNumber = $"{prefix}{currentSequence:D6}";

                // Map to CreateStudentRequest
                var createReq = new CreateStudentRequest(
                    req.FirstName, req.LastName, req.Email, req.Password, req.DateOfBirth, req.Gender,
                    req.GuardianFirstName, req.GuardianLastName, req.GuardianPhone, req.GuardianEmail,
                    req.MedicalNotes, req.PhotoUrl, req.ClassId
                );

                var (user, student, newGuardian, newGuardianUser) = await CreateStudentGuardianAndUserObject(
                    createReq, tenantId, studentNumber, guardianCache);

                if (newGuardianUser != null) guardianUsersToInsert.Add(newGuardianUser);
                if (newGuardian != null) guardiansToInsert.Add(newGuardian);
                
                usersToInsert.Add(user);
                studentsToInsert.Add(student);
            }

            if (guardianUsersToInsert.Any()) await _userRepository.AddUsersBulkAsync(guardianUsersToInsert);
            if (guardiansToInsert.Any()) await _guardianRepository.AddGuardiansBulkAsync(guardiansToInsert);
            
            await _userRepository.AddUsersBulkAsync(usersToInsert);
            await _studentRepository.AddStudentsBulkAsync(studentsToInsert);
        }

        var results = new BulkEnrollmentResult
        (
            requestList.Count,
            accepted.Count,
            failures.Count,
            failures
        );

        return BaseResponse<BulkEnrollmentResult>.SuccessResponse(
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

        // 2. Reserve the next sequence number for this school and year
        var nextSequence = await _tenantSequenceRepository.ReserveBlockAsync(tenantId, "STUDENT", year, 1);

        // 3. Return formatted ID with 6-digit padding (000001, 000002...)
        return $"{prefix}{nextSequence:D6}";
    }
}