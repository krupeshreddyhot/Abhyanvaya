namespace Abhyanvaya.Domain.Enums.Scheduling;

public enum TimetableChangeOperation : byte
{
    Create = 1,
    Update = 2,
    Delete = 3,
    Move = 4,
    Copy = 5,
    Clone = 6,
    Publish = 7,
    Archive = 8,
    Lock = 9,
    Unlock = 10,
    Freeze = 11,
    Unfreeze = 12,
}
