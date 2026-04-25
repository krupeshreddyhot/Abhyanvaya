using Abhyanvaya.API.Common;
using Abhyanvaya.API.Services;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Admin;
using Abhyanvaya.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AdminController : ControllerBase
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly CollegeBrandingService _branding;
        private readonly IConfiguration _configuration;

        public AdminController(
            IApplicationDbContext context,
            ICurrentUserService currentUser,
            CollegeBrandingService branding,
            IConfiguration configuration)
        {
            _context = context;
            _currentUser = currentUser;
            _branding = branding;
            _configuration = configuration;
        }

        /// <summary>
        /// List universities for dropdowns (admin UI). Same data as public login list but requires auth.
        /// </summary>
        [HttpGet("universities")]
        [Authorize(Policy = AuthorizationPolicies.UniversityListAccess)]
        public async Task<IActionResult> GetUniversities()
        {
            var list = await _context.Universities
                .AsNoTracking()
                .OrderBy(u => u.Name)
                .Select(u => new UniversityDto
                {
                    Id = u.Id,
                    Code = u.Code,
                    Name = u.Name
                })
                .ToListAsync();

            return Ok(list);
        }

        [HttpPost("universities")]
        [Authorize(Policy = AuthorizationPolicies.SuperAdminOnly)]
        public async Task<IActionResult> CreateUniversity([FromBody] CreateUniversityRequest request)
        {
            var code = request.Code.Trim().ToUpperInvariant();
            var name = request.Name.Trim();
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name))
                return BadRequest("University code and name are required");

            var exists = await _context.Universities.AnyAsync(u => u.Code.ToUpper() == code);
            if (exists)
                return BadRequest("University code already exists");

            var entity = new University
            {
                Code = code,
                Name = name,
                CreatedDate = DateTime.UtcNow
            };

            await _context.AddAsync(entity);
            await _context.SaveChangesAsync();

            return Ok(new UniversityDto { Id = entity.Id, Code = entity.Code, Name = entity.Name });
        }

        /// <summary>
        /// Colleges under a university that can be chosen as the parent (main campus),
        /// excluding the signed-in tenant college row. Uses university scope, not tenant filter.
        /// </summary>
        [HttpGet("parent-college-options")]
        [Authorize(Policy = AuthorizationPolicies.CanManageStudents)]
        public async Task<IActionResult> GetParentCollegeOptions([FromQuery] int universityId)
        {
            if (universityId <= 0)
                return BadRequest("University is required.");

            var universityExists = await _context.Universities.AnyAsync(u => u.Id == universityId);
            if (!universityExists)
                return BadRequest("Invalid university.");

            var ownCollege = await _context.Colleges
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == _currentUser.TenantId);

            if (ownCollege == null)
                return BadRequest("No college profile exists for this tenant yet.");

            var options = await _context.Colleges
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => !c.IsDeleted && c.UniversityId == universityId && c.Id != ownCollege.Id)
                .OrderBy(c => c.Name)
                .Select(c => new ParentCollegeOptionDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Code = c.Code,
                    ShortName = c.ShortName
                })
                .ToListAsync();

            return Ok(options);
        }

        /// <summary>
        /// College profile for the signed-in tenant (typically one row per tenant).
        /// </summary>
        [HttpGet("tenant-college")]
        [Authorize(Policy = AuthorizationPolicies.CanManageStudents)]
        public async Task<IActionResult> GetTenantCollege()
        {
            var college = await _context.Colleges
                .AsNoTracking()
                .Include(c => c.University)
                .Include(c => c.ParentCollege)
                .FirstOrDefaultAsync(c => c.TenantId == _currentUser.TenantId);

            if (college == null)
                return NotFound("No college profile for this tenant. Create one via onboarding or support.");

            return Ok(MapTenantCollege(college, BrandingSettingsResolver.Get(_configuration, "Branding:PublicBaseUrl")));
        }

        /// <summary>
        /// Upload a logo image; server saves WebP variants (max edge 64 / 128 / 256 px) under wwwroot/branding.
        /// </summary>
        [HttpPost("tenant-college/logo")]
        [Authorize(Policy = AuthorizationPolicies.CanManageStudents)]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> UploadTenantCollegeLogo(IFormFile? file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Choose an image file.");

            var (ok, error) = await _branding.SaveLogoForTenantAsync(_currentUser.TenantId, file, cancellationToken);
            if (!ok)
                return BadRequest(error);

            return Ok(new { message = "Logo saved (small, medium, large)." });
        }

        [HttpPut("tenant-college")]
        [Authorize(Policy = AuthorizationPolicies.CanManageStudents)]
        public async Task<IActionResult> UpdateTenantCollege([FromBody] UpdateTenantCollegeRequest request)
        {
            var name = request.Name.Trim();
            var code = request.Code.Trim().ToUpperInvariant();
            var shortName = request.ShortName?.Trim();
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(code))
                return BadRequest("College name and code are required");

            if (request.UniversityId <= 0)
                return BadRequest("University is required");

            var universityExists = await _context.Universities.AnyAsync(u => u.Id == request.UniversityId);
            if (!universityExists)
                return BadRequest("Invalid university");

            var college = await _context.Colleges
                .FirstOrDefaultAsync(c => c.TenantId == _currentUser.TenantId);

            if (college == null)
                return NotFound("No college profile for this tenant.");

            var duplicate = await _context.Colleges
                .IgnoreQueryFilters()
                .AnyAsync(c =>
                    c.Id != college.Id &&
                    !c.IsDeleted &&
                    c.UniversityId == request.UniversityId &&
                    c.Code.ToUpper() == code);

            if (duplicate)
                return BadRequest("Another college already uses this code under the selected university.");

            if (request.ParentCollegeId.HasValue)
            {
                var parentId = request.ParentCollegeId.Value;
                if (parentId == college.Id)
                    return BadRequest("Parent college cannot be the same college.");

                var parent = await _context.Colleges
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == parentId && !c.IsDeleted);

                if (parent == null)
                    return BadRequest("Parent college was not found.");

                if (parent.UniversityId != request.UniversityId)
                    return BadRequest("Parent college must belong to the same university.");
            }

            college.Name = name;
            college.ShortName = string.IsNullOrWhiteSpace(shortName) ? null : shortName;
            college.Code = code;
            college.UniversityId = request.UniversityId;
            college.ParentCollegeId = request.ParentCollegeId;

            await _context.SaveChangesAsync();
            return Ok();
        }

        private static TenantCollegeDto MapTenantCollege(College college, string? brandingPublicBaseUrl)
        {
            return new TenantCollegeDto
            {
                Id = college.Id,
                Name = college.Name,
                ShortName = college.ShortName,
                Code = college.Code,
                UniversityId = college.UniversityId,
                UniversityCode = college.University!.Code,
                UniversityName = college.University.Name,
                ParentCollegeId = college.ParentCollegeId,
                ParentCollegeName = college.ParentCollege?.Name,
                LogoSmPath = CollegeBrandingService.BuildLogoPath(college.LogoAccessKey, college.LogoUpdatedUtc, "sm", brandingPublicBaseUrl),
                LogoMdPath = CollegeBrandingService.BuildLogoPath(college.LogoAccessKey, college.LogoUpdatedUtc, "md", brandingPublicBaseUrl),
                LogoLgPath = CollegeBrandingService.BuildLogoPath(college.LogoAccessKey, college.LogoUpdatedUtc, "lg", brandingPublicBaseUrl),
            };
        }
    }
}
