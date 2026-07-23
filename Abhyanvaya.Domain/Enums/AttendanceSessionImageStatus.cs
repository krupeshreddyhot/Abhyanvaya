namespace Abhyanvaya.Domain.Enums;

/// <summary>Processing status of one classroom image within an AI attendance session (AI22.7A Phase 2).</summary>
public enum AttendanceSessionImageStatus : short
{
    Uploaded = 1,
    Processing = 2,
    Processed = 3,
    Failed = 4,
}
