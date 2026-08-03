using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations.Scheduling;

public sealed class TimetableConfiguration : IEntityTypeConfiguration<Timetable>
{
    public void Configure(EntityTypeBuilder<Timetable> builder)
    {
        builder.ToTable("SchedulingTimetable");
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Code).HasMaxLength(50);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.FreezeReason).HasMaxLength(2000);
        builder.Property(x => x.UnlockReason).HasMaxLength(2000);
        builder.Property(x => x.ArchiveComments).HasMaxLength(2000);
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.HasOne(x => x.AcademicYear).WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TimeSlotSet).WithMany().HasForeignKey(x => x.TimeSlotSetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ScheduleVersion).WithMany(v => v.Timetables).HasForeignKey(x => x.ScheduleVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ArchiveReason).WithMany().HasForeignKey(x => x.ArchiveReasonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReferenceVersion).WithMany().HasForeignKey(x => x.ReferenceVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Code }).IsUnique().HasFilter("\"Code\" IS NOT NULL");
        builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.DepartmentId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.IsFrozen });
    }
}
