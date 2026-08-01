using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations.Scheduling;

public sealed class FacultyDayPreferenceConfiguration : IEntityTypeConfiguration<FacultyDayPreference>
{
    public void Configure(EntityTypeBuilder<FacultyDayPreference> builder)
    {
        builder.ToTable("SchedulingFacultyDayPreference");
        builder.HasOne(x => x.FacultyWorkload).WithMany(y => y.DayPreferences).HasForeignKey(x => x.FacultyWorkloadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.FacultyWorkloadId, x.DayOfWeek }).IsUnique();
    }
}
