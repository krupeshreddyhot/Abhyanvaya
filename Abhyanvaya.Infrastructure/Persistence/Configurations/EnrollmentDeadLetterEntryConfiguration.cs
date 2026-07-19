using Abhyanvaya.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations;

public sealed class EnrollmentDeadLetterEntryConfiguration : IEntityTypeConfiguration<EnrollmentDeadLetterEntry>
{
    public void Configure(EntityTypeBuilder<EnrollmentDeadLetterEntry> builder)
    {
        builder.ToTable("EnrollmentDeadLetterEntry");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FailureReason)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.FailureCode)
            .HasMaxLength(100);

        builder.Property(x => x.ExceptionSummary)
            .HasMaxLength(2000);

        builder.Property(x => x.RetryHistoryJson)
            .HasColumnType("text");

        builder.Property(x => x.CreatedUtc)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => x.ItemId)
            .IsUnique()
            .HasDatabaseName("UX_EnrollmentDeadLetterEntry_Item");
    }
}
