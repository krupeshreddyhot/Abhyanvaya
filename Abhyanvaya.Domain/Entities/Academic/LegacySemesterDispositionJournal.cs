using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3E —
/// Auditable disposition journal for legacy NULL-group Semester finalization.
/// Does not replace Semester as SoT; records Architect-approved dispositions only.
/// </summary>
public class LegacySemesterDispositionJournal : BaseEntity
{
    public int SemesterId { get; set; }

    /// <summary>RETAIN_HISTORICAL | FINALIZED_LEGACY</summary>
    public string DispositionCode { get; set; } = "";

    /// <summary>Classifier evidence / approval baseline at finalization time.</summary>
    public string Evidence { get; set; } = "";

    public string PromptCode { get; set; } = "P1-4-3E";

    /// <summary>Set only when an approved GroupId assignment occurred (not used for RETAIN_HISTORICAL).</summary>
    public int? AssignedGroupId { get; set; }

    /// <summary>True only when Semester.GroupId (or other Semester columns) were mutated.</summary>
    public bool SemesterRowMutated { get; set; }

    public DateTime FinalizedUtc { get; set; }
}
