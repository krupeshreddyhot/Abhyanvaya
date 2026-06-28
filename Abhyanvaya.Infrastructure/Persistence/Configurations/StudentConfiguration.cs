using Abhyanvaya.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations;

public sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.Property(s => s.PhotoKey)
            .HasMaxLength(500)
            .HasColumnType("character varying(500)");

        builder.Property(s => s.PhotoUploadedUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(s => s.PhotoVerified)
            .HasDefaultValue(false);
    }
}
