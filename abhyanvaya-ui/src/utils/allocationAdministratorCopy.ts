/**
 * AI29.1D.24B / 24B.1 — Business-facing copy for Allocation Review UX.
 * Presentation only. Does not encode approval or placement rules.
 * Internal contracts (scenarioId, canApprove, replayAllocationScenario, etc.) stay unchanged.
 */

import type { ConstraintPriority } from "./allocationStrategyCatalog";
import {
  normalizeAllocationConstraintPriority,
  type AllocationConstraintPriorityRaw,
} from "./allocationConstraintPriority";

export type AllocationIssuePresentation = {
  title: string;
  description: string;
  /** Prefer rebuild workflow over refresh when true. */
  prefersRebuild?: boolean;
};

/** Display label for constraint priority values (string name or numeric enum from API). */
export function priorityDisplayLabel(priority: ConstraintPriority | AllocationConstraintPriorityRaw): string {
  const normalized = normalizeAllocationConstraintPriority(priority);
  if (normalized === "Mandatory") return "Required";
  if (normalized === "Preferred") return "Preferred";
  if (normalized === "Informational") return "Informational";
  return normalized || "Preferred";
}

export function priorityHelpText(priority: ConstraintPriority | AllocationConstraintPriorityRaw): string {
  switch (priorityDisplayLabel(priority)) {
    case "Required":
      return "The allocation must satisfy this rule.";
    case "Preferred":
      return "The allocation should satisfy this rule where possible.";
    case "Informational":
      return "This rule is considered for reporting but does not prevent approval.";
    default:
      return "";
  }
}

/** Map server/governance blocker text + flags into administrator language. */
export function presentAllocationIssue(input: {
  rawReason?: string | null;
  contextStale?: boolean;
  checksumInvalid?: boolean;
  concurrencyConflict?: boolean;
  authorizationFailure?: boolean;
}): AllocationIssuePresentation {
  const raw = (input.rawReason ?? "").trim();
  const lower = raw.toLowerCase();

  // Prefer explicit reason text before generic flags so archived/mandatory are not swallowed.
  if (/archiv/i.test(lower)) {
    return {
      title: "Allocation archived",
      description: "This allocation has been archived and cannot be approved.",
    };
  }

  if (/already approved|already been approved/i.test(lower)) {
    return {
      title: "Already approved",
      description: "This allocation has already been approved.",
    };
  }

  if (/mandatory|required|unresolved/i.test(lower) && !/stale|checksum|rebuild/i.test(lower)) {
    return {
      title: "Required allocation rules are not satisfied",
      description: sanitizeAdministratorMessage(raw) || "Resolve the required allocation issues before approval.",
    };
  }

  if (input.contextStale || /stale|earlier academic|outdated|rebuild/i.test(raw)) {
    return {
      title: "Allocation needs to be rebuilt",
      description:
        "The academic information used for this allocation has changed. Review the academic scope and generate the allocation again.",
      prefersRebuild: true,
    };
  }

  if (input.checksumInvalid || /checksum/i.test(raw)) {
    return {
      title: "Allocation data has changed",
      description:
        "The allocation can no longer be approved because its underlying data has changed. Review the academic scope and generate the allocation again.",
      prefersRebuild: true,
    };
  }

  if (input.concurrencyConflict || /concurren/i.test(raw)) {
    return {
      title: "Allocation was updated elsewhere",
      description:
        "This allocation was changed by another administrator. Refresh the allocation and review it again.",
    };
  }

  if (input.authorizationFailure || /authoriz|permission|forbidden/i.test(raw)) {
    return {
      title: "Approval not permitted",
      description: "You do not have permission to approve this allocation.",
    };
  }

  if (raw) {
    return {
      title: "Approval unavailable",
      description: sanitizeAdministratorMessage(raw),
    };
  }

  return {
    title: "Approval is currently unavailable",
    description: "Please refresh and try again.",
  };
}

const GUID_RE =
  /\b[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\b/gi;

/** Soften technical tokens that sometimes appear in API messages (normal admin path). */
export function sanitizeAdministratorMessage(message: string): string {
  return message
    .replace(GUID_RE, "")
    .replace(/\bcanApprove\b/gi, "approval eligibility")
    .replace(/\bblockingReasons\b/gi, "reasons")
    .replace(/\bchecksum\b/gi, "allocation data")
    .replace(/\bstale context\b/gi, "outdated academic information")
    .replace(/\bStudentSection\b/g, "student section assignments")
    .replace(/\bscenarioId\b/gi, "allocation reference")
    .replace(/\bscenario\b/gi, "allocation")
    .replace(/\bAllocation Engine\b/gi, "allocation service")
    .replace(/\bCapacity Engine\b/gi, "capacity service")
    .replace(/\bengine\b/gi, "service")
    .replace(/\bAI29\.1C(?:\.5A?)?\b/gi, "")
    .replace(/\s{2,}/g, " ")
    .replace(/\s+([.,;:])/g, "$1")
    .trim();
}

export function versionActionLabel(operation: string | null | undefined): string {
  const op = (operation ?? "").trim().toLowerCase();
  if (!op) return "Updated";
  if (op.includes("creat") || op.includes("generat") || op.includes("run")) return "Created";
  if (op.includes("simulat")) return "Tested";
  if (op.includes("review")) return "Reviewed";
  if (op.includes("approv")) return "Approved";
  if (op.includes("reject")) return "Rejected";
  if (op.includes("archiv")) return "Archived";
  if (op.includes("replay")) return "Replayed";
  if (op.includes("compar")) return "Compared";
  if (op.includes("save") || op.includes("draft")) return "Saved";
  return operation!.trim();
}

export const ALLOCATION_WORKSPACE_BANNER =
  "Use the steps below to prepare, test, review, and approve student allocation.";

export const MSG_TEST_ALLOCATION_SUCCESS = "Test allocation completed. No student records were changed.";
export const MSG_TEST_ALLOCATION_ERRORS =
  "Test allocation could not be completed. Review the issues below and try again.";
export const MSG_ALLOCATION_CREATED = "Allocation created successfully.";
export const MSG_COMPARE_COMPLETED = "Allocation comparison completed.";
export const MSG_DRAFT_SAVED = "Draft saved. Student records were not changed.";
/** Success toast after POST …/replay (existing replay API — not a full regenerate). */
export const MSG_REPLAY_COMPLETED = "Allocation replay completed. Student records were not changed.";
export const MSG_ALLOCATION_REVIEWED = "Allocation marked as reviewed.";
export const MSG_ALLOCATION_REJECTED = "Allocation rejected.";
export const MSG_ALLOCATION_ARCHIVED = "Allocation archived.";
export const MSG_NEED_PREVIEW_OR_TEST = "Run Preview or Test Allocation first.";
export const MSG_NEED_TEST_BEFORE_ALLOCATION =
  "Run Test Allocation first. Proposed assignments come from the server.";
export const MSG_NEED_PERMISSION_DRAFT = "You need permission to save a draft.";
/** Toast after Review Academic Scope navigation (no rebuild API). */
export const MSG_REVIEW_SCOPE_THEN_GENERATE =
  "The academic information used for this allocation has changed. Review the academic scope and generate the allocation again.";
/** Label for existing replayAllocationScenario — replay, not regenerate. */
export const LABEL_REPLAY_ALLOCATION = "Replay Allocation";
/** Stale-context navigation only — does not call run/replay APIs. */
export const LABEL_REVIEW_ACADEMIC_SCOPE = "Review Academic Scope";

export const APPROVE_CONFIRM_TITLE = "Approve this allocation for the next processing step?";
export const APPROVE_CONFIRM_DESCRIPTION =
  "Please confirm that you have reviewed the proposed student section assignments. Approval follows the server workflow and does not by itself permanently move students.";
