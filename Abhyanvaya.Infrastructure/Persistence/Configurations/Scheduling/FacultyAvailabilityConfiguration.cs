using Abhyanvaya.Domain.Entities.Scheduling;

using Microsoft.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore.Metadata.Builders;



namespace Abhyanvaya.Infrastructure.Persistence.Configurations.Scheduling;



public sealed class FacultyAvailabilityConfiguration : IEntityTypeConfiguration<FacultyAvailability>

{

    public void Configure(EntityTypeBuilder<FacultyAvailability> builder)

    {

        builder.ToTable("SchedulingFacultyAvailability");

        builder.Property(x => x.StartDate).HasColumnType("date");

        builder.Property(x => x.EndDate).HasColumnType("date");

        builder.Property(x => x.Reason).HasMaxLength(500);

        builder.Property(x => x.Remarks).HasMaxLength(1000);

        builder.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AcademicYear).WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.StartSlot).WithMany().HasForeignKey(x => x.StartSlotId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.EndSlot).WithMany().HasForeignKey(x => x.EndSlotId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TenantId, x.StaffId, x.AcademicYearId, x.StartDate, x.EndDate });

    }

}

