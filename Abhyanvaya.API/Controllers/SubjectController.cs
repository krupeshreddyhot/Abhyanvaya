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
        var list = await _context.Subjects
            .AsNoTracking()
            .OrderBy(x => x.Course!.Name)
            .ThenBy(x => x.Group!.Name)
            .ThenBy(x => x.Name)
            .Select(x => new SubjectCatalogDto
            {
                Id = x.Id,
                Name = x.Name,
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
                TeachingLanguageName = x.TeachingLanguage != null ? x.TeachingLanguage.Name : null
            })
            .ToListAsync();

        return Ok(list);
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
            .Select(x => new { x.Id, x.Name })
            .ToListAsync();

        var electiveSubjects = await _context.StudentSubjects
            .Where(x => x.StudentId == student.Id)
            .Select(x => new { x.Subject.Id, x.Subject.Name })
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
                Subjects = g.Select(x => new { x.Id, x.Name })
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
        var exists = await _context.Subjects
            .AnyAsync(x =>
                x.TenantId == _currentUser.TenantId &&
                x.CourseId == request.CourseId &&
                x.GroupId == request.GroupId &&
                x.SemesterId == request.SemesterId &&
                x.Name.ToLower() == request.Name.ToLower());

        if (exists)
            return BadRequest("A subject with this name already exists for this course, group and semester.");

        if (!await _context.Courses.AnyAsync(x => x.Id == request.CourseId))
            return BadRequest("Invalid Course");
        if (!await _context.Groups.AnyAsync(x => x.Id == request.GroupId))
            return BadRequest("Invalid Group");
        if (!await _context.Semesters.AnyAsync(x => x.Id == request.SemesterId))
            return BadRequest("Invalid Semester");

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
            Name = request.Name,
            CourseId = request.CourseId,
            GroupId = request.GroupId,
            SemesterId = request.SemesterId,
            IsElective = request.IsElective,
            ElectiveGroupId = electiveGroupId,
            LanguageSubjectSlot = request.LanguageSubjectSlot,
            TeachingLanguageId = teachingLanguageId,
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
                x.Name.ToLower() == request.Name.ToLower());

        if (duplicate)
            return BadRequest("A subject with this name already exists for this course, group and semester.");
        if (!await _context.Courses.AnyAsync(x => x.Id == request.CourseId))
            return BadRequest("Invalid course");
        if (!await _context.Groups.AnyAsync(x => x.Id == request.GroupId))
            return BadRequest("Invalid Group");
        if (!await _context.Semesters.AnyAsync(x => x.Id == request.SemesterId))
            return BadRequest("Invalid Semester");

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

        subject.Name = request.Name;        
        subject.CourseId = request.CourseId;
        subject.GroupId = request.GroupId;
        subject.SemesterId = request.SemesterId;
        subject.IsElective = request.IsElective;
        subject.ElectiveGroupId = electiveGroupId;
        subject.LanguageSubjectSlot = request.LanguageSubjectSlot;
        subject.TeachingLanguageId = teachingLanguageId;
        subject.UpdatedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _cache.RemoveAsync("master:subject");

        return Ok(subject);
    }
}
