using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations.Scheduling;

/// <summary>
/// AI-SCHED-TG.3 Prompt 2 — Authoritative TeachingGroup ↔ Section link (not student membership).
/// </summary>
public sealed class TeachingGroupSectionConfiguration : IEntityTypeConfiguration<TeachingGroupSection>
{
    public void Configure(EntityTypeBuilder<TeachingGroupSection> builder)
    {
        builder.ToTable("SchedulingTeachingGroupSection");

        builder.HasOne(x => x.TeachingGroup)
            .WithMany(x => x.Sections)
            .HasForeignKey(x => x.TeachingGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Section>()
            .WithMany()
            .HasForeignKey(x => x.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        // One link row per (TG, Section); soft-deleted rows may be re-linked later.
        builder.HasIndex(x => new { x.TenantId, x.TeachingGroupId, x.SectionId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = FALSE");

        builder.HasIndex(x => new { x.TenantId, x.SectionId });
    }
}
