namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Enrollment background processing host contract (AI20.PHASE2.2).
/// </summary>
public interface IEnrollmentBackgroundService
{
    bool IsRunning { get; }
}
