using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Exceptions;

namespace Abhyanvaya.Application.UnitTests.Scheduling.Phase2;

public sealed class TimetableEntryMappingTests
{
    [Fact]
    public void ApplyAllocationDenormalization_CopiesFieldsFromAllocation()
    {
        var allocation = new SubjectAllocation
        {
            Id = 100,
            StaffId = 11,
            SubjectId = 22,
            CourseId = 33,
            GroupId = 44,
            SemesterId = 55,
            DepartmentId = 66,
            PreferredRoomId = 77
        };

        var entry = new TimetableEntry();
        TimetableService.ApplyAllocationDenormalization(entry, allocation, 88, courseDepartmentId: 66);

        Assert.Equal(100, entry.SubjectAllocationId);
        Assert.Equal(11, entry.StaffId);
        Assert.Equal(22, entry.SubjectId);
        Assert.Equal(33, entry.CourseId);
        Assert.Equal(44, entry.GroupId);
        Assert.Equal(55, entry.SemesterId);
        Assert.Equal(66, entry.DepartmentId);
        Assert.Equal(88, entry.RoomId);
    }

    [Fact]
    public void ApplyAllocationDenormalization_UsesExplicitRoomOverPreferred()
    {
        var allocation = new SubjectAllocation
        {
            Id = 1,
            StaffId = 1,
            SubjectId = 1,
            CourseId = 1,
            GroupId = 1,
            SemesterId = 1,
            DepartmentId = 1,
            PreferredRoomId = 99
        };

        var entry = new TimetableEntry();
        TimetableService.ApplyAllocationDenormalization(entry, allocation, 42, courseDepartmentId: 1);
        Assert.Equal(42, entry.RoomId);
    }

    [Fact]
    public void ApplyAllocationDenormalization_Rejects_Department_Mismatch_With_Course()
    {
        var allocation = new SubjectAllocation
        {
            Id = 1,
            StaffId = 1,
            SubjectId = 1,
            CourseId = 1,
            GroupId = 1,
            SemesterId = 1,
            DepartmentId = 5,
        };
        var entry = new TimetableEntry();
        Assert.Throws<DomainException>(() =>
            TimetableService.ApplyAllocationDenormalization(entry, allocation, 42, courseDepartmentId: 9));
    }

    [Fact]
    public void ApplyAllocationDenormalization_Rejects_Requested_Entry_Department_Mismatch()
    {
        var allocation = new SubjectAllocation
        {
            Id = 1,
            StaffId = 1,
            SubjectId = 1,
            CourseId = 1,
            GroupId = 1,
            SemesterId = 1,
            DepartmentId = 5,
        };
        var entry = new TimetableEntry();
        Assert.Throws<DomainException>(() =>
            TimetableService.ApplyAllocationDenormalization(
                entry, allocation, 42, courseDepartmentId: 5, requestedEntryDepartmentId: 9));
    }
}
