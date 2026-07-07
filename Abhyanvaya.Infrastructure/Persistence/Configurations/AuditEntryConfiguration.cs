using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations;

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntry");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.EntityName)
            .HasMaxLength(128)
            .HasColumnType("character varying(128)");

        builder.Property(x => x.EntityId)
            .HasMaxLength(64)
            .HasColumnType("character varying(64)");

        builder.Property(x => x.Action)
            .HasConversion<int>();

        builder.Property(x => x.OldValues)
            .HasColumnType("jsonb");

        builder.Property(x => x.NewValues)
            .HasColumnType("jsonb");

        builder.Property(x => x.PerformedUtc)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("timezone('utc', now())");

        builder.HasIndex(x => new { x.TenantId, x.EntityName, x.EntityId })
            .HasDatabaseName("IX_AuditEntry_Tenant_Entity");

        builder.HasIndex(x => new { x.TenantId, x.PerformedUtc })
            .HasDatabaseName("IX_AuditEntry_Tenant_PerformedUtc");
    }
}
