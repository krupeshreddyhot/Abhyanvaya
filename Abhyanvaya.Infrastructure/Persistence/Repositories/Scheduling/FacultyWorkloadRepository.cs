using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Infrastructure.Persistence.Repositories.Scheduling;

public sealed class FacultyWorkloadRepository : IFacultyWorkloadRepository
{
    private readonly ApplicationDbContext _context;

    public FacultyWorkloadRepository(ApplicationDbContext context) => _context = context;

    public Task<FacultyWorkload?> GetByStaffIdAsync(int tenantId, int staffId, CancellationToken cancellationToken = default) =>
        _context.Set<FacultyWorkload>()
            .Include(x => x.DayPreferences)
            .Include(x => x.TimeSlotPreferences)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.StaffId == staffId, cancellationToken);

    public Task<FacultyWorkload?> GetByIdWithPreferencesAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>
        _context.Set<FacultyWorkload>()
            .Include(x => x.DayPreferences)
            .Include(x => x.TimeSlotPreferences)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public async Task AddAsync(FacultyWorkload entity, CancellationToken cancellationToken = default) =>
        await _context.Set<FacultyWorkload>().AddAsync(entity, cancellationToken);

    public Task<FacultyDayPreference?> GetDayPreferenceByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>
        _context.Set<FacultyDayPreference>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public async Task AddDayPreferenceAsync(FacultyDayPreference entity, CancellationToken cancellationToken = default) =>
        await _context.Set<FacultyDayPreference>().AddAsync(entity, cancellationToken);

    public Task<FacultyTimeSlotPreference?> GetTimeSlotPreferenceByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>
        _context.Set<FacultyTimeSlotPreference>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public async Task AddTimeSlotPreferenceAsync(FacultyTimeSlotPreference entity, CancellationToken cancellationToken = default) =>
        await _context.Set<FacultyTimeSlotPreference>().AddAsync(entity, cancellationToken);
}
