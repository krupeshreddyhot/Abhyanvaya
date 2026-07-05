using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations;

public sealed class AttendanceRecognitionReviewHistoryConfiguration
    : IEntityTypeConfiguration<AttendanceRecognitionReviewHistory>
{
    public void Configure(EntityTypeBuilder<AttendanceRecognitionReviewHistory> builder)
    {
        builder.ToTable("AttendanceRecognitionReviewHistory");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.OldStatus)
            .HasConversion<int>();

        builder.Property(x => x.NewStatus)
            .HasConversion<int>();

        builder.Property(x => x.ReviewAction)
            .HasConversion<int>();

        builder.Property(x => x.ReviewNotes)
            .HasMaxLength(500)
            .HasColumnType("character varying(500)");

        builder.Property(x => x.ReviewedUtc)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("timezone('utc', now())");

        builder.HasOne(x => x.Recognition)
            .WithMany(r => r.ReviewHistory)
            .HasForeignKey(x => x.RecognitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ReviewedByUser)
            .WithMany()
            .HasForeignKey(x => x.ReviewedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.RecognitionId, x.ReviewedUtc })
            .HasDatabaseName("IX_AttendanceRecognitionReviewHistory_Recognition_ReviewedUtc");

        builder.HasIndex(x => x.ReviewedBy)
            .HasDatabaseName("IX_AttendanceRecognitionReviewHistory_ReviewedBy");
    }
}
