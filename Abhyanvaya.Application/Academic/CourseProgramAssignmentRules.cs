namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI29.1D.24 Prompt 4A — pure rules for Course.ProgramId transitions (testable without EF).
/// </summary>
public static class CourseProgramAssignmentRules
{
    public sealed record ProgramSnapshot(int Id, int TenantId, bool IsActive, string Status);

    public sealed record Decision(
        bool IsNoOp,
        int? NextProgramId,
        bool PublishAssigned,
        bool PublishRemoved,
        bool InvalidateCaches,
        string? Error);

    public static int? NormalizeProgramId(int? programId)
        => programId is > 0 ? programId : null;

    public static bool IsAssignableForNewLink(ProgramSnapshot program)
        => program.IsActive
           && !string.Equals(program.Status, "Archived", StringComparison.OrdinalIgnoreCase)
           && !string.Equals(program.Status, "Inactive", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Evaluate transition when EnablePrograms = true.
    /// <paramref name="targetProgram"/> is required when next id &gt; 0 (must be tenant-matched by caller).
    /// </summary>
    public static Decision EvaluateEnabled(
        int? previousProgramId,
        int? requestedProgramId,
        ProgramSnapshot? targetProgram)
    {
        var next = NormalizeProgramId(requestedProgramId);
        var previous = NormalizeProgramId(previousProgramId);

        if (previous == next)
        {
            return new Decision(
                IsNoOp: true,
                NextProgramId: previous,
                PublishAssigned: false,
                PublishRemoved: false,
                InvalidateCaches: false,
                Error: null);
        }

        if (next is > 0)
        {
            if (targetProgram is null)
                return Fail("Invalid Program.");
            if (!IsAssignableForNewLink(targetProgram))
                return Fail("Archived or inactive Programs cannot receive new Courses.");
        }

        return new Decision(
            IsNoOp: false,
            NextProgramId: next,
            PublishAssigned: next is not null,
            PublishRemoved: previous is not null && next is null,
            InvalidateCaches: true,
            Error: null);
    }

    /// <summary>When Programs are disabled, force unlink; no-op if already null.</summary>
    public static Decision EvaluateDisabled(int? previousProgramId)
    {
        var previous = NormalizeProgramId(previousProgramId);
        if (previous is null)
        {
            return new Decision(
                IsNoOp: true,
                NextProgramId: null,
                PublishAssigned: false,
                PublishRemoved: false,
                InvalidateCaches: false,
                Error: null);
        }

        return new Decision(
            IsNoOp: false,
            NextProgramId: null,
            PublishAssigned: false,
            PublishRemoved: true,
            InvalidateCaches: true,
            Error: null);
    }

    private static Decision Fail(string error)
        => new(false, null, false, false, false, error);
}
