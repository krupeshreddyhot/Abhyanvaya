using Abhyanvaya.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Infrastructure.Persistence
{
    internal static class StaffHubModelConfigurator
    {
        public static void ConfigureStaffHub(ModelBuilder builder)
        {
            builder.Entity<Staff>().ToTable("StaffMembers");

            builder.Entity<Department>()
                .HasOne(d => d.College)
                .WithMany()
                .HasForeignKey(d => d.CollegeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Department>()
                .HasIndex(d => new { d.CollegeId, d.Code })
                .IsUnique();

            builder.Entity<Staff>()
                .HasOne(s => s.College)
                .WithMany()
                .HasForeignKey(s => s.CollegeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Staff>()
                .HasOne(s => s.StaffType)
                .WithMany()
                .HasForeignKey(s => s.StaffTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Staff>()
                .HasOne(s => s.PersonTitle)
                .WithMany()
                .HasForeignKey(s => s.PersonTitleId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Staff>()
                .HasOne(s => s.Designation)
                .WithMany()
                .HasForeignKey(s => s.DesignationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Staff>()
                .HasOne(s => s.Qualification)
                .WithMany()
                .HasForeignKey(s => s.QualificationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Staff>()
                .HasOne(s => s.Gender)
                .WithMany()
                .HasForeignKey(s => s.GenderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Staff>()
                .HasOne(s => s.EmploymentStatus)
                .WithMany()
                .HasForeignKey(s => s.EmploymentStatusId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Staff>()
                .HasIndex(s => new { s.CollegeId, s.StaffCode })
                .IsUnique()
                .HasFilter("\"StaffCode\" IS NOT NULL");

            builder.Entity<StaffDepartment>()
                .HasOne(x => x.Staff)
                .WithMany(s => s.StaffDepartments)
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<StaffDepartment>()
                .HasOne(x => x.Department)
                .WithMany()
                .HasForeignKey(x => x.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<StaffDepartment>()
                .HasIndex(x => new { x.StaffId, x.DepartmentId })
                .IsUnique();

            builder.Entity<StaffDepartmentRole>()
                .HasOne(x => x.StaffDepartment)
                .WithMany(d => d.StaffDepartmentRoles)
                .HasForeignKey(x => x.StaffDepartmentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<StaffDepartmentRole>()
                .HasOne(x => x.DepartmentRoleLookup)
                .WithMany()
                .HasForeignKey(x => x.DepartmentRoleLookupId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<StaffDepartmentRole>()
                .HasIndex(x => new { x.StaffDepartmentId, x.DepartmentRoleLookupId })
                .IsUnique();

            builder.Entity<StaffCollegeRole>()
                .HasOne(x => x.Staff)
                .WithMany(s => s.StaffCollegeRoles)
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<StaffCollegeRole>()
                .HasOne(x => x.CollegeRoleLookup)
                .WithMany()
                .HasForeignKey(x => x.CollegeRoleLookupId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<StaffCollegeRole>()
                .HasIndex(x => new { x.StaffId, x.CollegeRoleLookupId })
                .IsUnique();

            builder.Entity<StaffSubjectAssignment>()
                .HasOne(x => x.Staff)
                .WithMany(s => s.StaffSubjectAssignments)
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<StaffSubjectAssignment>()
                .HasOne(x => x.Subject)
                .WithMany()
                .HasForeignKey(x => x.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<StaffSubjectAssignment>()
                .HasIndex(x => new { x.StaffId, x.SubjectId })
                .IsUnique();

            builder.Entity<User>()
                .HasOne(u => u.Staff)
                .WithMany()
                .HasForeignKey(u => u.StaffId)
                .OnDelete(DeleteBehavior.SetNull);

            // RBAC (Permission is not BaseEntity — no tenant query filter)
            builder.Entity<Permission>()
                .HasIndex(p => p.Key)
                .IsUnique();

            builder.Entity<ApplicationRole>()
                .HasIndex(r => new { r.TenantId, r.Code })
                .IsUnique();

            builder.Entity<ApplicationRolePermission>()
                .HasKey(x => new { x.ApplicationRoleId, x.PermissionId });

            builder.Entity<ApplicationRolePermission>()
                .HasOne(x => x.ApplicationRole)
                .WithMany(r => r.ApplicationRolePermissions)
                .HasForeignKey(x => x.ApplicationRoleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ApplicationRolePermission>()
                .HasOne(x => x.Permission)
                .WithMany()
                .HasForeignKey(x => x.PermissionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<UserApplicationRole>()
                .HasKey(x => new { x.UserId, x.ApplicationRoleId });

            builder.Entity<UserApplicationRole>()
                .HasOne(x => x.User)
                .WithMany(u => u.UserApplicationRoles)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserApplicationRole>()
                .HasOne(x => x.ApplicationRole)
                .WithMany(r => r.UserApplicationRoles)
                .HasForeignKey(x => x.ApplicationRoleId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
