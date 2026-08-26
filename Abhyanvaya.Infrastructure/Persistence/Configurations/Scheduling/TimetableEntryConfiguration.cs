using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations.Scheduling;

public sealed class TimetableEntryConfiguration : IEntityTypeConfiguration<TimetableEntry>
{
    public void Configure(EntityTypeBuilder<TimetableEntry> builder)
    {
        builder.ToTable("SchedulingTimetableEntry");
        builder.Property(x => x.Remarks).HasMaxLength(500);
        builder.HasOne(x => x.Timetable).WithMany(t => t.Entries).HasForeignKey(x => x.TimetableId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.TimeSlot).WithMany().HasForeignKey(x => x.TimeSlotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SubjectAllocation).WithMany().HasForeignKey(x => x.SubjectAllocationId).OnDelete(DeleteBehavior.Restrict);

        // AI-SCHED-TG.4 Prompt 2 — TeachingGroup 1──* TimetableEntry; Restrict (do not cascade-delete entries).
        builder.HasOne(x => x.TeachingGroup)
            .WithMany()
            .HasForeignKey(x => x.TeachingGroupId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Semester).WithMany().HasForeignKey(x => x.SemesterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.TimetableId, x.DayOfWeek, x.TimeSlotId });
        builder.HasIndex(x => new { x.TenantId, x.TimetableId, x.StaffId });
        builder.HasIndex(x => new { x.TenantId, x.TimetableId, x.RoomId });
        builder.HasIndex(x => new { x.TenantId, x.TimetableId, x.CourseId, x.GroupId, x.SemesterId });
        builder.HasIndex(x => new { x.TenantId, x.TeachingGroupId });
    }
}
