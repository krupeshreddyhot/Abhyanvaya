using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Infrastructure.Persistence.Repositories;

public sealed class StudentRepository : IStudentRepository
{
    private readonly ApplicationDbContext _context;

    public StudentRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Student?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Students.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<Student?> GetByIdForTenantAsync(int id, int tenantId, CancellationToken cancellationToken = default) =>
        _context.Students.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId, cancellationToken);
}
