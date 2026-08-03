namespace Abhyanvaya.Domain.Enums.Scheduling;

[Flags]
public enum RoomFeatureFlags : short
{
    None = 0,
    AiCamera = 1,
    Projector = 2,
    Wifi = 4,
    SmartBoard = 8,
    SmartClassroom = 16,
}
