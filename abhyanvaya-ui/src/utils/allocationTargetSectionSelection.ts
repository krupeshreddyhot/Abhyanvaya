/**
 * AI29.1D.24B.2 — Target Section selection helpers (UI contract only).
 * Eligibility remains Allocation Context / server authority — no Course/Group string filters.
 */

export type TargetSectionMode = "all" | "explicit";

/** null = all eligible (server contract); non-null = explicit selected ids. */
export function targetSectionMode(targetSectionIds: number[] | null | undefined): TargetSectionMode {
  return targetSectionIds === null || targetSectionIds === undefined ? "all" : "explicit";
}

/**
 * Continue from Section Capacity / Target Sections.
 * `targetSectionIds = null` means "All eligible" only when at least one eligible Section exists.
 * Does not change the server 10A contract (null still means all-on-server when a run is submitted).
 */
export function canContinueWithTargetSections(
  targetSectionIds: number[] | null | undefined,
  eligibleSectionCount: number,
): boolean {
  if (eligibleSectionCount <= 0) return false;
  if (targetSectionIds === null || targetSectionIds === undefined) return true;
  return targetSectionIds.length > 0;
}

export function selectedTargetSectionCount(targetSectionIds: number[] | null | undefined): number {
  if (targetSectionIds === null || targetSectionIds === undefined) return 0;
  return targetSectionIds.length;
}

export function formatSelectedTargetSectionsLabel(count: number): string {
  return `Selected: ${count} section${count === 1 ? "" : "s"}`;
}

export const MSG_SELECT_AT_LEAST_ONE_SECTION = "Select at least one Section to continue.";
export const MSG_NO_ELIGIBLE_SECTIONS =
  "No eligible Sections are available for the selected academic scope.";
export const MSG_UNABLE_TO_LOAD_ELIGIBLE_SECTIONS = "Unable to load eligible Sections.";
export const MSG_TARGET_SECTIONS_HELPER =
  "Choose whether allocation should use all eligible Sections or only selected Sections.";
export const MSG_ALL_ELIGIBLE_HELPER = "Uses all Sections available for the selected academic scope.";
export const MSG_EXPLICIT_HELPER = "Select one or more Sections.";

/** Stable scope key for clearing stale targetSectionIds when parents change. */
export function allocationScopeKey(scope: {
  academicYearId?: number | null;
  programId?: number | null;
  courseId?: number | null;
  groupId?: number | null;
  semesterId?: number | null;
} | null | undefined): string {
  if (!scope) return "";
  return [
    scope.academicYearId ?? "",
    scope.programId ?? "",
    scope.courseId ?? "",
    scope.groupId ?? "",
    scope.semesterId ?? "",
  ].join("|");
}

/** Filter occupancy rows to authoritative context section ids — never return unfiltered catalog. */
export function filterOccupancyToContextSections<T extends { sectionId: number }>(
  rows: readonly T[],
  contextSectionIds: ReadonlySet<number>,
): T[] {
  if (contextSectionIds.size === 0) return [];
  return rows.filter((r) => contextSectionIds.has(r.sectionId));
}

export function toggleExplicitSectionId(
  current: number[] | null,
  sectionId: number,
  checked: boolean,
): number[] | null {
  // Switching from "all" to explicit: start empty then apply the click.
  const base = current === null ? [] : [...current];
  const set = new Set(base);
  if (checked) set.add(sectionId);
  else set.delete(sectionId);
  return [...set].sort((a, b) => a - b);
}
