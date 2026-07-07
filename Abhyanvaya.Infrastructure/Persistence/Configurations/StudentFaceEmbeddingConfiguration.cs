using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations;

public sealed class StudentFaceEmbeddingConfiguration : IEntityTypeConfiguration<StudentFaceEmbedding>
{
    public void Configure(EntityTypeBuilder<StudentFaceEmbedding> builder)
    {
        builder.ToTable("StudentFaceEmbedding");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.EmbeddingVector)
            .HasColumnType("real[]");

        builder.Property(x => x.EmbeddingModel)
            .HasMaxLength(128)
            .HasColumnType("character varying(128)");

        builder.Property(x => x.EmbeddingVersion)
            .HasMaxLength(64)
            .HasColumnType("character varying(64)");

        builder.Property(x => x.EmbeddingStatus)
            .HasConversion<int>()
            .HasDefaultValue(EmbeddingStatus.Pending);

        builder.Property(x => x.EmbeddingQuality)
            .HasConversion<int>()
            .HasDefaultValue(EmbeddingQuality.Unknown);

        builder.Property(x => x.EmbeddingDimension)
            .HasDefaultValue(0);

        builder.Property(x => x.PhotoVersion)
            .HasDefaultValue(0L);

        builder.Property(x => x.RetryCount)
            .HasDefaultValue(0);

        builder.Property(x => x.LastFailureUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.LastFailureReason)
            .HasMaxLength(500)
            .HasColumnType("character varying(500)");

        builder.Property(x => x.PhotoKey)
            .HasMaxLength(500)
            .HasColumnType("character varying(500)");

        builder.Property(x => x.GeneratedUtc)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("timezone('utc', now())");

        builder.Property(x => x.IsActive)
            .HasDefaultValue(false);

        builder.HasOne(x => x.Student)
            .WithMany(s => s.FaceEmbeddings)
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.StudentId })
            .HasDatabaseName("IX_StudentFaceEmbedding_Tenant_Student");

        builder.HasIndex(x => new { x.StudentId, x.IsActive })
            .IsUnique()
            .HasFilter("\"IsActive\" = true")
            .HasDatabaseName("IX_StudentFaceEmbedding_Student_Active");
    }
}
