/**
 * AI29.1D — Display helpers for AI29.1C.5A scenario lifecycle.
 * AI29.1D.24B — administrator-facing blockers; does not encode approval rules.
 */

import { presentAllocationIssue, type AllocationIssuePresentation } from "./allocationAdministratorCopy";

/** Canonical governance lifecycle states shown in the workspace. */
export const GOVERNANCE_LIFECYCLE_STATES = [
  "Draft",
  "Review",
  "Approved",
  "Rejected",
  "Archived",
] as const;

export type GovernanceLifecycleDisplay = (typeof GOVERNANCE_LIFECYCLE_STATES)[number] | string;

/**
 * Map engine lifecycle tokens to the five UI-facing governance states.
 * Intermediate engine states (Simulated, Compared, Saved, …) normalize toward Draft/Review.
 */
export function toGovernanceLifecycleDisplay(raw: string | null | undefined): GovernanceLifecycleDisplay {
  const v = (raw ?? "").trim();
  if (!v) return "Draft";
  const lower = v.toLowerCase();
  if (lower === "draft" || lower === "generated" || lower === "saved" || lower === "simulated" || lower === "simulationaccepted") {
    return "Draft";
  }
  if (lower === "reviewed" || lower === "review" || lower === "compared" || lower === "underreview") {
    return "Review";
  }
  if (lower === "approved") return "Approved";
  if (lower === "rejected") return "Rejected";
  if (lower === "archived") return "Archived";
  return v;
}

export function lifecycleChipColor(
  display: GovernanceLifecycleDisplay,
): "default" | "info" | "success" | "error" | "warning" {
  switch (display) {
    case "Draft":
      return "default";
    case "Review":
      return "info";
    case "Approved":
      return "success";
    case "Rejected":
      return "error";
    case "Archived":
      return "warning";
    default:
      return "default";
  }
}

export function executionStatusLabel(raw: string | null | undefined): string {
  const v = (raw ?? "").trim();
  if (!v) return "Not started";
  const lower = v.toLowerCase();
  if (lower === "completed" || lower === "success") return "Completed";
  if (lower === "failed" || lower === "error") return "Failed";
  if (lower === "running" || lower === "inprogress") return "In progress";
  return v;
}

/** Collect authoritative blocker strings from governance evaluate/approve payloads. */
export function governanceBlockingReasons(gov: {
  blockingReasons?: string[] | null;
  errors?: string[] | null;
  message?: string | null;
  canApprove?: boolean;
  contextStale?: boolean;
  checksumInvalid?: boolean;
  concurrencyConflict?: boolean;
  authorizationFailure?: boolean;
} | null | undefined): string[] {
  return governanceBlockingPresentations(gov).map((p) => `${p.title}: ${p.description}`);
}

/** Business-facing presentations derived from server governance payload (no new rules). */
export function governanceBlockingPresentations(gov: {
  blockingReasons?: string[] | null;
  errors?: string[] | null;
  message?: string | null;
  canApprove?: boolean;
  contextStale?: boolean;
  checksumInvalid?: boolean;
  concurrencyConflict?: boolean;
  authorizationFailure?: boolean;
} | null | undefined): AllocationIssuePresentation[] {
  if (!gov) return [];

  const rawReasons = [...(gov.blockingReasons ?? [])];
  for (const e of gov.errors ?? []) {
    if (e && !rawReasons.includes(e)) rawReasons.push(e);
  }

  const presentations: AllocationIssuePresentation[] = [];
  const seenTitles = new Set<string>();

  const push = (p: AllocationIssuePresentation) => {
    if (seenTitles.has(p.title)) return;
    seenTitles.add(p.title);
    presentations.push(p);
  };

  if (gov.contextStale) {
    push(presentAllocationIssue({ contextStale: true }));
  }
  if (gov.checksumInvalid) {
    push(presentAllocationIssue({ checksumInvalid: true }));
  }
  if (gov.concurrencyConflict) {
    push(presentAllocationIssue({ concurrencyConflict: true }));
  }
  if (gov.authorizationFailure) {
    push(presentAllocationIssue({ authorizationFailure: true }));
  }

  // Map each server reason on its own text — flags already contributed presentations above.
  for (const raw of rawReasons) {
    push(presentAllocationIssue({ rawReason: raw }));
  }

  if (!gov.canApprove && presentations.length === 0) {
    push(presentAllocationIssue({ rawReason: gov.message }));
  }

  return presentations;
}

export function governancePrefersRebuild(gov: {
  contextStale?: boolean;
  checksumInvalid?: boolean;
  blockingReasons?: string[] | null;
} | null | undefined): boolean {
  if (!gov) return false;
  if (gov.contextStale || gov.checksumInvalid) return true;
  return (gov.blockingReasons ?? []).some((r) => /stale|checksum|rebuild|outdated|earlier academic/i.test(r));
}
