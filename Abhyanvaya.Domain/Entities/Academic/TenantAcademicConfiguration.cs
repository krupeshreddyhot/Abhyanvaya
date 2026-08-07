using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Academic;

/// <summary>
/// AI29.1A — Tenant-level academic hierarchy configuration.
/// When <see cref="EnablePrograms"/> is false, hierarchy is College → Course (ProgramId stays null).
/// </summary>
public class TenantAcademicConfiguration : BaseEntity
{
    public int CollegeId { get; set; }
    public bool EnablePrograms { get; set; }
}
