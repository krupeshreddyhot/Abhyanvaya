using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations.Scheduling;

/// <summary>
/// AI-SCHED-TG.3 Prompt 2 — TeachingGroup persistence.
/// Many TeachingGroups per SubjectAllocation; Section links only via TeachingGroupSection.
/// </summary>
public sealed class TeachingGroupConfiguration : IEntityTypeConfiguration<TeachingGroup>
{
    public void Configure(EntityTypeBuilder<TeachingGroup> builder)
    {
        builder.ToTable("SchedulingTeachingGroup");

        builder.Property(x => x.Code).HasMaxLength(50);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ExclusionGroupKey).HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.EffectiveFrom).HasColumnType("date");
        builder.Property(x => x.EffectiveTo).HasColumnType("date");

        builder.Property(x => x.Type).HasConversion<byte>();
        builder.Property(x => x.MembershipSource).HasConversion<byte>();
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.ActivityKind).HasConversion<byte>();

        // Capacity: nullable planning/ceiling fields — no PlannedCapacity / ResolvedStudentCount columns.
        builder.Property(x => x.ExpectedStudentCount);
        builder.Property(x => x.MaxTeachingCapacity);

        builder.HasOne(x => x.SubjectAllocation)
            .WithMany()
            .HasForeignKey(x => x.SubjectAllocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AcademicYear>()
            .WithMany()
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Course>()
            .WithMany()
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Semester>()
            .WithMany()
            .HasForeignKey(x => x.SemesterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Subject>()
            .WithMany()
            .HasForeignKey(x => x.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        // Child relationships configured on TeachingGroupSection / TeachingGroupMembership.

        // Access: list TGs for an allocation (many per SubjectAllocation — NOT unique).
        builder.HasIndex(x => new { x.TenantId, x.SubjectAllocationId });

        // Access: mutual-exclusion peer lookup by shared ExclusionGroupKey (key is NOT unique).
        builder.HasIndex(x => new { x.TenantId, x.SubjectAllocationId, x.ExclusionGroupKey });

        builder.HasIndex(x => new { x.TenantId, x.Status });
    }
}
