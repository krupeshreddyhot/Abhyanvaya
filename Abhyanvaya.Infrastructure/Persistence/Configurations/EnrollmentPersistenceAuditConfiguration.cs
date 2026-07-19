using Abhyanvaya.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations;

public sealed class EnrollmentPersistenceAuditConfiguration : IEntityTypeConfiguration<EnrollmentPersistenceAudit>
{
    public void Configure(EntityTypeBuilder<EnrollmentPersistenceAudit> builder)
    {
        builder.ToTable("EnrollmentPersistenceAudit");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.ModelVersion).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Outcome).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Detail).HasMaxLength(1000);
        builder.Property(x => x.TimestampUtc).HasColumnType("timestamp with time zone");

        builder.HasOne(x => x.EnrollmentItem)
            .WithMany()
            .HasForeignKey(x => x.EnrollmentItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.EnrollmentItemId, x.TimestampUtc })
            .HasDatabaseName("IX_EnrollmentPersistenceAudit_Item_Timestamp");
    }
}
