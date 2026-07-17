using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Infrastructure.Persistence.Repositories;

public sealed class EnrollmentProgressSnapshotRepository : IEnrollmentProgressSnapshotRepository
{
    private readonly ApplicationDbContext _context;

    public EnrollmentProgressSnapshotRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AppendAsync(
        StudentEnrollmentProgressSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        await _context.Set<StudentEnrollmentProgressSnapshot>().AddAsync(snapshot, cancellationToken);
    }
}
