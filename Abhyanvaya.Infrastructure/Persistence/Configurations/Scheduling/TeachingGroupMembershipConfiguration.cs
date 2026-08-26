using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations.Scheduling;

/// <summary>
/// AI-SCHED-TG.3 Prompt 2 — Explicit operational membership (Include/Exclude).
/// Temporal history allowed; only one current membership per student per TeachingGroup.
/// Does not relate to StudentSection / StudentSubject.
/// </summary>
public sealed class TeachingGroupMembershipConfiguration : IEntityTypeConfiguration<TeachingGroupMembership>
{
    public void Configure(EntityTypeBuilder<TeachingGroupMembership> builder)
    {
        builder.ToTable("SchedulingTeachingGroupMembership");

        builder.Property(x => x.Inclusion).HasConversion<byte>();
        builder.Property(x => x.EffectiveFrom).HasColumnType("date");
        builder.Property(x => x.EffectiveTo).HasColumnType("date");

        builder.HasOne(x => x.TeachingGroup)
            .WithMany(x => x.Memberships)
            .HasForeignKey(x => x.TeachingGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Student>()
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Historical rows allowed (EffectiveFrom/EffectiveTo); prevent duplicate current rows.
        builder.HasIndex(x => new { x.TenantId, x.TeachingGroupId, x.StudentId })
            .IsUnique()
            .HasFilter("\"IsCurrent\" = TRUE AND \"IsDeleted\" = FALSE");

        builder.HasIndex(x => new { x.TenantId, x.TeachingGroupId });
        builder.HasIndex(x => new { x.TenantId, x.StudentId });
    }
}
