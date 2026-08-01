using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations.Scheduling;

public sealed class FacultyTeachingPreferenceConfiguration : IEntityTypeConfiguration<FacultyTeachingPreference>
{
    public void Configure(EntityTypeBuilder<FacultyTeachingPreference> builder)
    {
        builder.ToTable("SchedulingFacultyTeachingPreference");
        builder.Property(x => x.Remarks).HasMaxLength(1000);
        builder.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicYear).WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PreferredCampus).WithMany().HasForeignKey(x => x.PreferredCampusId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PreferredBuilding).WithMany().HasForeignKey(x => x.PreferredBuildingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PreferredFloor).WithMany().HasForeignKey(x => x.PreferredFloorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PreferredRoom).WithMany().HasForeignKey(x => x.PreferredRoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PreferredSubject).WithMany().HasForeignKey(x => x.PreferredSubjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PreferredDepartment).WithMany().HasForeignKey(x => x.PreferredDepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PreferredCourse).WithMany().HasForeignKey(x => x.PreferredCourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PreferredGroup).WithMany().HasForeignKey(x => x.PreferredGroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PreferredSemester).WithMany().HasForeignKey(x => x.PreferredSemesterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.StaffId, x.AcademicYearId, x.IsActive });
    }
}

public sealed class RoomFeatureConfiguration : IEntityTypeConfiguration<RoomFeature>
{
    public void Configure(EntityTypeBuilder<RoomFeature> builder)
    {
        builder.ToTable("SchedulingRoomFeature");
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

public sealed class RoomFeatureAssignmentConfiguration : IEntityTypeConfiguration<RoomFeatureAssignment>
{
    public void Configure(EntityTypeBuilder<RoomFeatureAssignment> builder)
    {
        builder.ToTable("SchedulingRoomFeatureAssignment");
        builder.HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RoomFeature).WithMany(y => y.Assignments).HasForeignKey(x => x.RoomFeatureId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.RoomId, x.RoomFeatureId }).IsUnique();
    }
}

public sealed class SubjectDeliveryTypeConfiguration : IEntityTypeConfiguration<SubjectDeliveryType>
{
    public void Configure(EntityTypeBuilder<SubjectDeliveryType> builder)
    {
        builder.ToTable("SchedulingSubjectDeliveryType");
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

public sealed class HolidayTypeCatalogConfiguration : IEntityTypeConfiguration<HolidayTypeCatalog>
{
    public void Configure(EntityTypeBuilder<HolidayTypeCatalog> builder)
    {
        builder.ToTable("SchedulingHolidayTypes");
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Colour).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

public sealed class SubjectDeliveryExtensionsConfiguration : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        builder.Property(x => x.RequiresAttendance).HasDefaultValue(true);

        builder.HasOne(x => x.DeliveryType)
            .WithMany()
            .HasForeignKey(x => x.DeliveryTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PreferredRoomFeature)
            .WithMany()
            .HasForeignKey(x => x.PreferredRoomFeatureId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class HolidayPhase1BExtensionsConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> builder)
    {
        builder.Property(x => x.Colour).HasMaxLength(20);
        builder.HasOne(x => x.HolidayTypeCatalog)
            .WithMany()
            .HasForeignKey(x => x.HolidayTypeCatalogId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
