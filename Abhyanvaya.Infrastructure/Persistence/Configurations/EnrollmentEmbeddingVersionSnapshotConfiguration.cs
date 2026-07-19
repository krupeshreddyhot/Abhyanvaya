using Abhyanvaya.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations;

public sealed class EnrollmentEmbeddingVersionSnapshotConfiguration : IEntityTypeConfiguration<EnrollmentEmbeddingVersionSnapshot>
{
    public void Configure(EntityTypeBuilder<EnrollmentEmbeddingVersionSnapshot> builder)
    {
        builder.ToTable("EnrollmentEmbeddingVersionSnapshot");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.EmbeddingModel).HasMaxLength(128).IsRequired();
        builder.Property(x => x.EmbeddingModelVersion).HasMaxLength(128).IsRequired();
        builder.Property(x => x.FrameworkVersion).HasMaxLength(128);
        builder.Property(x => x.OnnxVersion).HasMaxLength(64);
        builder.Property(x => x.CreatedUtc).HasColumnType("timestamp with time zone");

        builder.HasOne(x => x.StudentFaceEmbedding)
            .WithMany()
            .HasForeignKey(x => x.StudentFaceEmbeddingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.EnrollmentItem)
            .WithMany()
            .HasForeignKey(x => x.EnrollmentItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.StudentFaceEmbeddingId)
            .IsUnique()
            .HasDatabaseName("UX_EnrollmentEmbeddingVersionSnapshot_Embedding");
    }
}
