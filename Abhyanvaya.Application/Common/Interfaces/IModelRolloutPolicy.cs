using Abhyanvaya.Application.ModelLifecycle;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Rollout policy plugin for model deployment (AI20.PHASE2.5).</summary>
public interface IModelRolloutPolicy
{
    RolloutPolicyType PolicyType { get; }

    bool CanApply(RolloutRequest request);

    AIModelState TargetState { get; }
}
