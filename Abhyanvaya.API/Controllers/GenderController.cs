using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Lookup;
using Abhyanvaya.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.API.Controllers;

[ApiController]
[Route("api/gender")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class GenderController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GenderController(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var data = await _context.Genders
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name })
            .ToListAsync();

        return Ok(data);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateGenderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Name is required.");

        var name = request.Name.Trim();
        var exists = await _context.Genders.AnyAsync(x =>
            x.TenantId == _currentUser.TenantId &&
            x.Name.ToLower() == name.ToLower());

        if (exists)
            return BadRequest("This gender already exists.");

        var entity = new Gender
        {
            Name = name,
            CreatedDate = DateTime.UtcNow
        };

        await _context.AddAsync(entity);
        await _context.SaveChangesAsync();

        return Ok(entity);
    }

    [HttpPut]
    public async Task<IActionResult> Update(UpdateGenderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Name is required.");

        var entity = await _context.Genders.FirstOrDefaultAsync(x => x.Id == request.Id);
        if (entity == null)
            return NotFound();

        var name = request.Name.Trim();
        var duplicate = await _context.Genders.AnyAsync(x =>
            x.Id != request.Id &&
            x.TenantId == _currentUser.TenantId &&
            x.Name.ToLower() == name.ToLower());

        if (duplicate)
            return BadRequest("Another gender already uses this name.");

        entity.Name = name;
        entity.UpdatedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(entity);
    }
}
