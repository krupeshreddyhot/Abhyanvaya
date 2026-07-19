namespace Abhyanvaya.Domain.Enums;

public enum EnrollmentWorkerState
{
    Idle = 0,
    Polling = 1,
    Claiming = 2,
    Running = 3,
    RenewingLease = 4,
    Completed = 5,
    Retrying = 6,
    Failed = 7,
    Cancelled = 8,
    Stopped = 9,
}
