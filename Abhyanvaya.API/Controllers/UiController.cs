using Abhyanvaya.API.Services;
using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Abhyanvaya.API.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.API.Controllers
{
    [ApiController]
    [Route("api/ui")]
    [Authorize(Policy = AuthorizationPolicies.TenantScopedUser)]
    public class UiController : ControllerBase
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly IConfiguration _configuration;

        public UiController(
            IApplicationDbContext context,
            ICurrentUserService currentUser,
            IConfiguration configuration)
        {
            _context = context;
            _currentUser = currentUser;
            _configuration = configuration;
        }

        [HttpGet("me")]
        public IActionResult GetCurrentUser()
        {
            return Ok(new
            {
                _currentUser.UserId,
                _currentUser.Role,
                _currentUser.TenantId,
                _currentUser.CourseId,
                _currentUser.GroupId
            });
        }

        [HttpGet("header")]
        public async Task<IActionResult> GetHeaderInfo()
        {
            var college = await _context.Colleges
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == _currentUser.TenantId);

            var fullName = string.IsNullOrWhiteSpace(college?.Name) ? "College" : college!.Name;
            var shortName = string.IsNullOrWhiteSpace(college?.ShortName) ? fullName : college!.ShortName!;

            var brandingPublicBaseUrl = BrandingSettingsResolver.Get(_configuration, "Branding:PublicBaseUrl");
            return Ok(new
            {
                fullName,
                shortName,
                role = _currentUser.Role,
                logoSmPath = CollegeBrandingService.BuildLogoPath(college?.LogoAccessKey, college?.LogoUpdatedUtc, "sm", brandingPublicBaseUrl),
                logoMdPath = CollegeBrandingService.BuildLogoPath(college?.LogoAccessKey, college?.LogoUpdatedUtc, "md", brandingPublicBaseUrl),
                logoLgPath = CollegeBrandingService.BuildLogoPath(college?.LogoAccessKey, college?.LogoUpdatedUtc, "lg", brandingPublicBaseUrl),
            });
        }
    }
}
