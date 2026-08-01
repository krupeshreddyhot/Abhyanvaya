using Abhyanvaya.Domain.Entities;

using Abhyanvaya.Domain.Entities.Scheduling;

using Microsoft.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore.Metadata.Builders;



namespace Abhyanvaya.Infrastructure.Persistence.Configurations.Scheduling;



public sealed class SubjectCategoryConfiguration : IEntityTypeConfiguration<SubjectCategory>

{

    public void Configure(EntityTypeBuilder<SubjectCategory> builder)

    {

        builder.ToTable("SchedulingSubjectCategory");

        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();

    }

}



public sealed class SubjectSchedulingExtensionsConfiguration : IEntityTypeConfiguration<Subject>

{

    public void Configure(EntityTypeBuilder<Subject> builder)

    {

        builder.HasOne(x => x.SubjectCategory)

            .WithMany()

            .HasForeignKey(x => x.SubjectCategoryId)

            .OnDelete(DeleteBehavior.Restrict);

    }

}

