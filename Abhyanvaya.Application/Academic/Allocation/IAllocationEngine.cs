namespace Abhyanvaya.Application.Academic.Allocation;

/// <summary>
/// AI29.1C placeholder. Implementations MUST only consume <see cref="SectionAllocationContext"/>
/// (no direct Capacity/Student/Section repository access).
/// </summary>
public interface IAllocationEngine
{
    string EngineCode { get; }
}

/// <summary>No-op engine until AI29.1C ships algorithms.</summary>
public sealed class NullAllocationEngine : IAllocationEngine
{
    public string EngineCode => "Null";
}
