using Abhyanvaya.Domain.Entities.Scheduling;

using Microsoft.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore.Metadata.Builders;



namespace Abhyanvaya.Infrastructure.Persistence.Configurations.Scheduling;



public sealed class RoomAvailabilityConfiguration : IEntityTypeConfiguration<RoomAvailability>

{

    public void Configure(EntityTypeBuilder<RoomAvailability> builder)

    {

        builder.ToTable("SchedulingRoomAvailability");

        builder.Property(x => x.StartDate).HasColumnType("date");

        builder.Property(x => x.EndDate).HasColumnType("date");

        builder.Property(x => x.Reason).HasMaxLength(500);

        builder.HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AcademicYear).WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.StartSlot).WithMany().HasForeignKey(x => x.StartSlotId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.EndSlot).WithMany().HasForeignKey(x => x.EndSlotId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TenantId, x.RoomId, x.AcademicYearId, x.StartDate, x.EndDate });

    }

}

