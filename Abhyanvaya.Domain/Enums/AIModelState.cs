namespace Abhyanvaya.Domain.Enums;

/// <summary>Lifecycle state of an AI model version (AI20.PHASE2.5).</summary>
public enum AIModelState
{
    Draft = 0,
    Testing = 1,
    Benchmarking = 2,
    Approved = 3,
    Canary = 4,
    Production = 5,
    Deprecated = 6,
    Retired = 7,
    RolledBack = 8,
}
