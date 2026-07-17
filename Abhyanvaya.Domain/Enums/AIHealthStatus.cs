namespace Abhyanvaya.Domain.Enums;

/// <summary>AI platform health status (AI20.PHASE2.6).</summary>
public enum AIHealthStatus
{
    Ready = 0,
    Live = 1,
    Degraded = 2,
    Maintenance = 3,
    Offline = 4,
}
