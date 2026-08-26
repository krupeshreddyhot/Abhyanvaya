using Abhyanvaya.Application.Academic;
using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Course;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.API.Controllers
{
    /// <summary>
    /// Course Master CRUD. When EnablePrograms, ProgramId is applied via
    /// <see cref="IAcademicStructureService.AssignCourseToProgramAsync"/> (authoritative assign-course command)
    /// inside <see cref="ICourseMasterWriteService"/> transaction orchestration — UI must not also call assign-course.
    /// </summary>
    [ApiController]
    [Route("api/course")]
    [Authorize(Policy = AuthorizationPolicies.CanManageCourses)]
    public class CourseController : ControllerBase
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly ICourseMasterWriteService _writeService;
        private readonly IAuthorizationService _authorization;

        public CourseController(
            IApplicationDbContext context,
            ICurrentUserService currentUser,
            ICourseMasterWriteService writeService,
            IAuthorizationService authorization)
        {
            _context = context;
            _currentUser = currentUser;
            _writeService = writeService;
            _authorization = authorization;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var data = await _context.Courses
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new { x.Id, x.Code, x.Name, x.DepartmentId, x.ProgramId })
                .ToListAsync();

            return Ok(data);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateCourseRequest request, CancellationToken cancellationToken)
        {
            var forbid = await EnsureProgramAssignAuthorizedAsync(request.ProgramIdSpecified && request.ProgramId is > 0, cancellationToken);
            if (forbid is not null)
                return forbid;

            try
            {
                var row = await _writeService.CreateAsync(request, cancellationToken);
                return Ok(row);
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Course not found.");
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
            // Unexpected exceptions (incl. assignment failures) are not swallowed — global pipeline returns failure.
        }

        [HttpPut]
        public async Task<IActionResult> Update(UpdateCourseRequest request, CancellationToken cancellationToken)
        {
            // Explicit programId (incl. null unlink) requires assign authorization; omitted ⇒ leave Program alone.
            var needsAssignAuth = request.ProgramIdSpecified;
            var forbid = await EnsureProgramAssignAuthorizedAsync(needsAssignAuth, cancellationToken);
            if (forbid is not null)
                return forbid;

            try
            {
                var row = await _writeService.UpdateAsync(request, cancellationToken);
                return Ok(row);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// When Program assignment will run, enforce CanAssignCourseToProgram before any Course mutation.
        /// </summary>
        private async Task<IActionResult?> EnsureProgramAssignAuthorizedAsync(
            bool assignmentRequested,
            CancellationToken cancellationToken)
        {
            if (!assignmentRequested)
                return null;

            if (!await ProgramsEnabledAsync(cancellationToken))
                return null;

            var auth = await _authorization.AuthorizeAsync(User, AuthorizationPolicies.CanAssignCourseToProgram);
            if (!auth.Succeeded)
                return Forbid();

            return null;
        }

        private async Task<bool> ProgramsEnabledAsync(CancellationToken cancellationToken)
        {
            return await _context.TenantAcademicConfigurations.AsNoTracking()
                .Where(c => c.TenantId == _currentUser.TenantId)
                .Select(c => c.EnablePrograms)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
