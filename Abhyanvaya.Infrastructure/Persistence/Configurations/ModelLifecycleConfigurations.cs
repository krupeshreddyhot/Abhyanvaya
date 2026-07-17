using Abhyanvaya.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations;

public sealed class AiModelDefinitionConfiguration : IEntityTypeConfiguration<AiModelDefinition>
{
    public void Configure(EntityTypeBuilder<AiModelDefinition> builder)
    {
        builder.ToTable("AiModelDefinitions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ModelKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ModelType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => x.ModelKey).IsUnique();
    }
}

public sealed class AiModelVersionConfiguration : IEntityTypeConfiguration<AiModelVersion>
{
    public void Configure(EntityTypeBuilder<AiModelVersion> builder)
    {
        builder.ToTable("AiModelVersions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Version).HasMaxLength(64).IsRequired();
        builder.Property(x => x.EmbeddingVersion).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RecognitionVersion).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Checksum).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => new { x.ModelDefinitionId, x.Version }).IsUnique();
        builder.HasIndex(x => x.IsActive);
        builder.HasOne(x => x.ModelDefinition).WithMany(x => x.Versions).HasForeignKey(x => x.ModelDefinitionId);
    }
}

public sealed class GoldenDatasetDefinitionConfiguration : IEntityTypeConfiguration<GoldenDatasetDefinition>
{
    public void Configure(EntityTypeBuilder<GoldenDatasetDefinition> builder)
    {
        builder.ToTable("GoldenDatasetDefinitions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DatasetKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Version).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => new { x.DatasetKey, x.Version }).IsUnique();
    }
}

public sealed class ModelRolloutPlanConfiguration : IEntityTypeConfiguration<ModelRolloutPlan>
{
    public void Configure(EntityTypeBuilder<ModelRolloutPlan> builder)
    {
        builder.ToTable("ModelRolloutPlans");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RolloutKey).HasMaxLength(128).IsRequired();
        builder.HasOne(x => x.ModelVersion).WithMany().HasForeignKey(x => x.ModelVersionId);
    }
}

public sealed class ModelLifecycleAuditEntryConfiguration : IEntityTypeConfiguration<ModelLifecycleAuditEntry>
{
    public void Configure(EntityTypeBuilder<ModelLifecycleAuditEntry> builder)
    {
        builder.ToTable("ModelLifecycleAuditEntries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasMaxLength(64).IsRequired();
        builder.Property(x => x.FromVersion).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(512).IsRequired();
    }
}

public sealed class RetrainingCandidateEntryConfiguration : IEntityTypeConfiguration<RetrainingCandidateEntry>
{
    public void Configure(EntityTypeBuilder<RetrainingCandidateEntry> builder)
    {
        builder.ToTable("RetrainingCandidateEntries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Source).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CorrectionType).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.TenantId);
    }
}
