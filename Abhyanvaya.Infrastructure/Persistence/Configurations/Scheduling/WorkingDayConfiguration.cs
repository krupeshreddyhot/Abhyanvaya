using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations.Scheduling;

public sealed class WorkingDayConfiguration : IEntityTypeConfiguration<WorkingDay>
{
    public void Configure(EntityTypeBuilder<WorkingDay> builder)
    {
        builder.ToTable("SchedulingWorkingDay");
        builder.HasOne(x => x.AcademicYear).WithMany(y => y.WorkingDays).HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.DayOfWeek }).IsUnique();
    }
}
