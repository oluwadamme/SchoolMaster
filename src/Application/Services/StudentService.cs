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
    private readonly ICurrentTenant _currentTenant;

    public StudentService(
        IUserRepository userRepository, 
        IStudentRepository studentRepository, 
        IGuardianRepository guardianRepository,
        ITenantRepository tenantRepository,
        ICurrentTenant currentTenant)
    {
        _userRepository = userRepository;
        _studentRepository = studentRepository;
        _guardianRepository = guardianRepository;
        _tenantRepository = tenantRepository;
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

        var (user, student, newGuardian, newGuardianUser) = await CreateStudentAndUserObject(request, tenantId);
        
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

    private async Task<(User user, Student student, Guardian? newGuardian, User? newGuardianUser)> CreateStudentAndUserObject(CreateStudentRequest request, Guid tenantId)
    {
        // 1. Check if guardian exists by email
        var existingGuardian = await _guardianRepository.GetGuardianByEmailAsync(request.GuardianEmail, tenantId);
        
        Guardian guardianToLink;
        Guardian? newGuardian = null;
        User? newGuardianUser = null;

        if (existingGuardian != null)
        {
            guardianToLink = existingGuardian;
        }
        else
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
        }

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

        // 1. Make a list of request student numbers and fetch all student numbers that already exist in the database
        var studentNumbers = requestList.Select(x => x.StudentNumber.Trim()).Distinct().ToList();
        var existingStudentNumbers = await _studentRepository.GetExistingStudentNumbersAsync(studentNumbers, tenantId);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var accepted = new List<(int Row, BulkEnrollStudentItemRequest Req)>();
        var failures = new List<BulkEnrollmentFailure>();

        for (var i = 0; i < requestList.Count; i++)
        {
            var studentNumber = requestList[i].StudentNumber.Trim();
            if (existingStudentNumbers.Contains(studentNumber) || !seen.Add(studentNumber))
            {
                failures.Add(new BulkEnrollmentFailure(i + 1, requestList[i].StudentNumber, "Student number already exists."));
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
            var existingGuardians = existingGuardiansList.ToDictionary(g => g.Email.ToLowerInvariant());

            // Track new guardians created in this batch to avoid duplicates
            var newGuardiansCache = new Dictionary<string, Guardian>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < accepted.Count; i++)
            {
                var req = accepted[i].Req;
                var normalizedGuardianEmail = req.GuardianEmail.Trim().ToLowerInvariant();

                Guardian guardianToLink;
                if (existingGuardians.TryGetValue(normalizedGuardianEmail, out var existingG))
                {
                    guardianToLink = existingG;
                }
                else if (newGuardiansCache.TryGetValue(normalizedGuardianEmail, out var cachedG))
                {
                    guardianToLink = cachedG;
                }
                else
                {
                    // Create new Guardian and User for Guardian
                    var newGuardianUser = User.Create(
                        tenantId: tenantId,
                        status: UserStatus.Active,
                        roles: new List<UserRole> { UserRole.Parent },
                        firstName: req.GuardianFirstName,
                        lastName: req.GuardianLastName,
                        email: req.GuardianEmail,
                        passwordHash: BCrypt.Net.BCrypt.HashPassword("DefaultPassword123!") // Or another strategy
                    );

                    var newGuardian = new Guardian
                    {
                        Id = Guid.NewGuid(),
                        UserId = newGuardianUser.Id,
                        TenantId = tenantId,
                        FirstName = req.GuardianFirstName,
                        LastName = req.GuardianLastName,
                        Phone = req.GuardianPhone,
                        Email = req.GuardianEmail
                    };

                    guardianUsersToInsert.Add(newGuardianUser);
                    guardiansToInsert.Add(newGuardian);
                    newGuardiansCache[normalizedGuardianEmail] = newGuardian;
                    guardianToLink = newGuardian;
                }

                var user = User.Create(
                    tenantId: tenantId,
                    status: UserStatus.Active,
                    roles: new List<UserRole> { UserRole.Student },
                    firstName: req.FirstName,
                    lastName: req.LastName,
                    email: req.Email,
                    passwordHash: BCrypt.Net.BCrypt.HashPassword(req.Password)
                );

                var student = new Student
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    TenantId = tenantId,
                    ClassId = req.ClassId,
                    StudentNumber = req.StudentNumber,
                    FirstName = req.FirstName,
                    LastName = req.LastName,
                    DateOfBirth = req.DateOfBirth,
                    Gender = req.Gender,
                    GuardianId = guardianToLink.Id,
                    Guardian = guardianToLink,
                    MedicalNotes = req.MedicalNotes,
                    PhotoUrl = req.PhotoUrl,
                    Status = StudentStatus.Active,
                    EnrolledAt = DateTime.UtcNow
                };

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