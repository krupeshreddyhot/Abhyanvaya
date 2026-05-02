using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs;
using Abhyanvaya.Application.DTOs.Semester;
using Abhyanvaya.Application.DTOs.Subject;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.API.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AuthorizationPolicies.TenantScopedUser)]
public class SubjectController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public SubjectController(IApplicationDbContext context, ICurrentUserService currentUser, ICacheService cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    [HttpGet("catalog")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> GetCatalog()
    {
        var isAdmin = _currentUser.Role.Equals(nameof(UserRole.Admin), StringComparison.OrdinalIgnoreCase);
        var list = await _context.Subjects
            .AsNoTracking()
            .OrderBy(x => x.Course!.Name)
            .ThenBy(x => x.Group!.Name)
            .ThenBy(x => x.TenantSubject!.Name)
            .Select(x => new SubjectCatalogDto
            {
                Id = x.Id,
                TenantSubjectId = x.TenantSubjectId,
                Code = x.TenantSubject != null ? x.TenantSubject.Code : null,
                Name = x.TenantSubject != null ? x.TenantSubject.Name : "",
                CourseId = x.CourseId,
                CourseName = x.Course != null ? x.Course.Name : "",
                GroupId = x.GroupId,
                GroupName = x.Group != null ? x.Group.Name : "",
                SemesterId = x.SemesterId,
                SemesterName = x.Semester != null ? x.Semester.Name : "",
                IsElective = x.IsElective,
                ElectiveGroupId = x.ElectiveGroupId,
                ElectiveGroupName = x.ElectiveGroup != null ? x.ElectiveGroup.Name : null,
                LanguageSubjectSlot = x.LanguageSubjectSlot,
                TeachingLanguageId = x.TeachingLanguageId,
                TeachingLanguageName = x.TeachingLanguage != null ? x.TeachingLanguage.Name : null,
                HPW = isAdmin ? x.HPW : null,
                Credits = isAdmin ? x.Credits : null,
                ExamHours = isAdmin ? x.ExamHours : null,
                Marks = isAdmin ? x.Marks : null
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("tenant-lookup")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> GetTenantLookup([FromQuery] string? q = null)
    {
        var query = _context.TenantSubjects.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var raw = q.Trim();
            // PostgreSQL ILIKE for reliable case-insensitive match on name and optional code.
            var pattern = "%" + EscapeLikePattern(raw) + "%";
            query = query.Where(x =>
                EF.Functions.ILike(x.Name, pattern, "\\") ||
                (x.Code != null && EF.Functions.ILike(x.Code, pattern, "\\")));
        }

        var list = await query
            .OrderBy(x => x.Name)
            .Take(50)
            .Select(x => new TenantSubjectDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpPost("tenant-lookup")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> CreateTenantLookup(CreateTenantSubjectRequest request)
    {
        var name = (request.Name ?? string.Empty).Trim();
        var code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Subject name is required.");

        var duplicate = await _context.TenantSubjects.AnyAsync(x =>
            x.TenantId == _currentUser.TenantId &&
            (x.Name.ToLower() == name.ToLower() ||
             (!string.IsNullOrWhiteSpace(code) && x.Code != null && x.Code.ToLower() == code.ToLower())));
        if (duplicate)
            return BadRequest("A tenant subject with this name or code already exists.");

        var tenantSubject = new TenantSubject
        {
            Name = name,
            Code = code
        };

        await _context.AddAsync(tenantSubject);
        await _context.SaveChangesAsync();
        await _cache.RemoveAsync("master:subject");
        return Ok(new TenantSubjectDto { Id = tenantSubject.Id, Name = tenantSubject.Name, Code = tenantSubject.Code });
    }

    [HttpGet("my-subjects")]
    public async Task<IActionResult> GetMySubjects()
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(x => x.Id == _currentUser.UserId);

        if (student == null)
            return NotFound("Student not found");

        var subjects = _context.Subjects
            .Where(x =>
                x.CourseId == student.CourseId &&
                x.GroupId == student.GroupId &&
                x.SemesterId == student.SemesterId);

        var coreSubjects = await subjects
            .Where(x => !x.IsElective)
            .Where(x =>
                x.LanguageSubjectSlot == SubjectLanguageSlot.None ||
                (x.LanguageSubjectSlot == SubjectLanguageSlot.FirstLanguage &&
                 x.TeachingLanguageId == student.FirstLanguageId) ||
                (x.LanguageSubjectSlot == SubjectLanguageSlot.SecondLanguage &&
                 x.TeachingLanguageId == student.LanguageId))
            .Select(x => new { x.Id, Name = x.TenantSubject != null ? x.TenantSubject.Name : "" })
            .ToListAsync();

        var electiveSubjects = await _context.StudentSubjects
            .Where(x => x.StudentId == student.Id)
            .Select(x => new { x.Subject.Id, Name = x.Subject.TenantSubject != null ? x.Subject.TenantSubject.Name : "" })
            .ToListAsync();

        var result = coreSubjects.Concat(electiveSubjects);

        return Ok(result);
    }
    [HttpGet("electives")]
    public async Task<IActionResult> GetElectives()
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(x => x.Id == _currentUser.UserId);

        if (student == null)
            return NotFound();

        var electives = await _context.Subjects
            .Where(x =>
                x.CourseId == student.CourseId &&
                x.GroupId == student.GroupId &&
                x.SemesterId == student.SemesterId &&
                x.IsElective)
            .GroupBy(x => x.ElectiveGroup.Name)
            .Select(g => new
            {
                Group = g.Key,
                Subjects = g.Select(x => new { x.Id, Name = x.TenantSubject != null ? x.TenantSubject.Name : "" })
            })
            .ToListAsync();

        return Ok(electives);
    }
    [HttpPost("select")]
    public async Task<IActionResult> SelectSubjects([FromBody] SelectSubjectsRequest request)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(x => x.Id == _currentUser.UserId);

        if (student == null)
            return NotFound("Student not found");

        if (request.SubjectIds == null || !request.SubjectIds.Any())
            return BadRequest("No subjects selected");

        // Get valid subjects for student
        var validSubjects = await _context.Subjects
            .Where(x =>
                x.CourseId == student.CourseId &&
                x.GroupId == student.GroupId &&
                x.SemesterId == student.SemesterId &&
                x.IsElective)
            .ToListAsync();

        var validSubjectIds = validSubjects.Select(x => x.Id).ToHashSet();

        // Validation 1: Subject belongs to student
        if (request.SubjectIds.Any(id => !validSubjectIds.Contains(id)))
            return BadRequest("Invalid subject selection");

        //  Validation 2: No duplicates
        if (request.SubjectIds.Count != request.SubjectIds.Distinct().Count())
            return BadRequest("Duplicate subjects selected");

        //  Validation 3: One per ElectiveGroup
        var selectedSubjects = validSubjects
            .Where(x => request.SubjectIds.Contains(x.Id))
            .ToList();

        var grouped = selectedSubjects
            .GroupBy(x => x.ElectiveGroupId);

        if (grouped.Any(g => g.Count() > 1))
            return BadRequest("Only one subject allowed per elective group");

        // Remove old selections
        var existing = _context.StudentSubjects
            .Where(x => x.StudentId == student.Id);

        foreach (var item in existing)
        {
            _context.Remove(item);
        }

        // Add new selections
        foreach (var subjectId in request.SubjectIds)
        {
            var studentSubject = new StudentSubject
            {
                StudentId = student.Id,
                SubjectId = subjectId,
                TenantId = student.TenantId,
                CreatedDate = DateTime.UtcNow
            };

            await _context.AddAsync(studentSubject);
        }

        await _context.SaveChangesAsync();

        return Ok("Subjects selected successfully");
    }
    // ADD
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Create(CreateSubjectRequest request)
    {
        if (request.TenantSubjectId <= 0)
            return BadRequest("Tenant subject is required.");

        var exists = await _context.Subjects
            .AnyAsync(x =>
                x.TenantId == _currentUser.TenantId &&
                x.CourseId == request.CourseId &&
                x.GroupId == request.GroupId &&
                x.SemesterId == request.SemesterId &&
                x.TenantSubjectId == request.TenantSubjectId);

        if (exists)
            return BadRequest("This subject is already assigned to the selected course, group and semester.");

        if (!await _context.Courses.AnyAsync(x => x.Id == request.CourseId))
            return BadRequest("Invalid Course");
        if (!await _context.Groups.AnyAsync(x => x.Id == request.GroupId))
            return BadRequest("Invalid Group");
        if (!await _context.Semesters.AnyAsync(x => x.Id == request.SemesterId))
            return BadRequest("Invalid Semester");
        if (!await _context.TenantSubjects.AnyAsync(x => x.Id == request.TenantSubjectId))
            return BadRequest("Invalid tenant subject.");

        int? electiveGroupId = null;
        if (request.IsElective)
        {
            if (!request.ElectiveGroupId.HasValue || request.ElectiveGroupId.Value <= 0)
                return BadRequest("Elective group is required for elective subjects.");
            if (!await _context.ElectiveGroups.AnyAsync(x => x.Id == request.ElectiveGroupId.Value))
                return BadRequest("Invalid Elective Group");
            electiveGroupId = request.ElectiveGroupId.Value;
        }

        int? teachingLanguageId = null;
        if (request.LanguageSubjectSlot is SubjectLanguageSlot.FirstLanguage or SubjectLanguageSlot.SecondLanguage)
        {
            if (!request.TeachingLanguageId.HasValue || request.TeachingLanguageId.Value <= 0)
                return BadRequest("Teaching language is required for first/second language subjects.");
            if (!await _context.Languages.AnyAsync(l =>
                    l.Id == request.TeachingLanguageId.Value &&
                    l.TenantId == _currentUser.TenantId))
                return BadRequest("Invalid teaching language for this tenant.");
            teachingLanguageId = request.TeachingLanguageId.Value;
        }
        else if (request.TeachingLanguageId.HasValue && request.TeachingLanguageId.Value > 0)
            return BadRequest("Teaching language should only be set when the language slot is first or second language.");

        var subject = new Subject
        {
            TenantSubjectId = request.TenantSubjectId,
            CourseId = request.CourseId,
            GroupId = request.GroupId,
            SemesterId = request.SemesterId,
            IsElective = request.IsElective,
            ElectiveGroupId = electiveGroupId,
            LanguageSubjectSlot = request.LanguageSubjectSlot,
            TeachingLanguageId = teachingLanguageId,
            HPW = request.HPW,
            Credits = request.Credits,
            ExamHours = request.ExamHours,
            Marks = request.Marks,
            CreatedDate = DateTime.UtcNow
        };

        await _context.AddAsync(subject);
        await _context.SaveChangesAsync();

        // invalidate cache
        await _cache.RemoveAsync("master:subject");

        return Ok(subject);
    }

    // UPDATE
    [HttpPut]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Update(UpdateSubjectRequest request)
    {
        if (request.TenantSubjectId <= 0)
            return BadRequest("Tenant subject is required.");

        var subject = await _context.Subjects.FirstOrDefaultAsync(x => x.Id == request.Id);

        if (subject == null)
            return NotFound();

        var duplicate = await _context.Subjects
            .AnyAsync(x =>
                x.Id != request.Id &&
                x.TenantId == _currentUser.TenantId &&
                x.CourseId == request.CourseId &&
                x.GroupId == request.GroupId &&
                x.SemesterId == request.SemesterId &&
                x.TenantSubjectId == request.TenantSubjectId);

        if (duplicate)
            return BadRequest("This subject is already assigned to the selected course, group and semester.");
        if (!await _context.Courses.AnyAsync(x => x.Id == request.CourseId))
            return BadRequest("Invalid course");
        if (!await _context.Groups.AnyAsync(x => x.Id == request.GroupId))
            return BadRequest("Invalid Group");
        if (!await _context.Semesters.AnyAsync(x => x.Id == request.SemesterId))
            return BadRequest("Invalid Semester");
        if (!await _context.TenantSubjects.AnyAsync(x => x.Id == request.TenantSubjectId))
            return BadRequest("Invalid tenant subject.");

        int? electiveGroupId = null;
        if (request.IsElective)
        {
            if (!request.ElectiveGroupId.HasValue || request.ElectiveGroupId.Value <= 0)
                return BadRequest("Elective group is required for elective subjects.");
            if (!await _context.ElectiveGroups.AnyAsync(x => x.Id == request.ElectiveGroupId.Value))
                return BadRequest("Invalid Elective Group");
            electiveGroupId = request.ElectiveGroupId.Value;
        }

        int? teachingLanguageId = null;
        if (request.LanguageSubjectSlot is SubjectLanguageSlot.FirstLanguage or SubjectLanguageSlot.SecondLanguage)
        {
            if (!request.TeachingLanguageId.HasValue || request.TeachingLanguageId.Value <= 0)
                return BadRequest("Teaching language is required for first/second language subjects.");
            if (!await _context.Languages.AnyAsync(l =>
                    l.Id == request.TeachingLanguageId.Value &&
                    l.TenantId == _currentUser.TenantId))
                return BadRequest("Invalid teaching language for this tenant.");
            teachingLanguageId = request.TeachingLanguageId.Value;
        }
        else if (request.TeachingLanguageId.HasValue && request.TeachingLanguageId.Value > 0)
            return BadRequest("Teaching language should only be set when the language slot is first or second language.");

        subject.TenantSubjectId = request.TenantSubjectId;
        subject.CourseId = request.CourseId;
        subject.GroupId = request.GroupId;
        subject.SemesterId = request.SemesterId;
        subject.IsElective = request.IsElective;
        subject.ElectiveGroupId = electiveGroupId;
        subject.LanguageSubjectSlot = request.LanguageSubjectSlot;
        subject.TeachingLanguageId = teachingLanguageId;
        subject.HPW = request.HPW;
        subject.Credits = request.Credits;
        subject.ExamHours = request.ExamHours;
        subject.Marks = request.Marks;
        subject.UpdatedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _cache.RemoveAsync("master:subject");

        return Ok(subject);
    }

    /// <summary>Escape % and _ for use inside ILIKE patterns.</summary>
    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }
}
