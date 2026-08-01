namespace Abhyanvaya.Domain.Enums.Scheduling;

public enum VersionDifferenceKind : byte
{
    Added = 1,
    Removed = 2,
    Modified = 3,
}

public enum VersionDifferenceCategory : byte
{
    AddedEntry = 1,
    RemovedEntry = 2,
    FacultyAssignment = 3,
    RoomAssignment = 4,
    SubjectAssignment = 5,
    PeriodChange = 6,
    TimeSlotChange = 7,
    Other = 8,
}

public enum ArchiveReasonCode : byte
{
    Superseded = 1,
    SemesterComplete = 2,
    Correction = 3,
    Emergency = 4,
    AcademicCouncil = 5,
    Other = 6,
}
