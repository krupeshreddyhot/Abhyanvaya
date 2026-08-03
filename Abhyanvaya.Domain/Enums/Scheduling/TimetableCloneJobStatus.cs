namespace Abhyanvaya.Domain.Enums.Scheduling;

public enum TimetableCloneJobStatus : byte
{
    Queued = 1,
    Running = 2,
    Completed = 3,
    Failed = 4,
}
