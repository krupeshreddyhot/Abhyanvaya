namespace Abhyanvaya.Domain.Common;

/// <summary>
/// Marks an entity as owned by a single college tenant.
/// Used by types that are not <see cref="BaseEntity"/> (e.g. <see cref="Entities.AttendanceSession"/>)
/// so the persistence layer can apply a row-level tenant filter.
/// </summary>
public interface ITenantScoped
{
    int TenantId { get; set; }
}
