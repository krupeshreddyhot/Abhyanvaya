using Abhyanvaya.Application.ClassroomAttendance;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Manual review routing for unknown/borderline faces — no UI (AI20.PHASE2.4).</summary>
public interface IManualReviewService
{
    ManualReviewResult Evaluate(ManualReviewRequest request);
}
