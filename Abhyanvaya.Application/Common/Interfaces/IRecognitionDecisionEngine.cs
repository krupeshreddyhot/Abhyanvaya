using Abhyanvaya.Application.Recognition;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Applies recognition policy — returns decision only (AI20.PHASE2.3).</summary>
public interface IRecognitionDecisionEngine
{
    RecognitionDecision Decide(RecognitionDecisionContext context, IReadOnlySet<int>? alreadyAssignedStudentIds = null);
}
