/**
 * AI-SCHED-TG.6 Prompt 4 Prompt 2 — Timetable Teaching Group selector contract helpers.
 * Transport/policy only — no UI selector, no compatibility algorithm.
 */

/** Architecture guard: UI must never filter Teaching Groups for timetable compatibility. */
export const shouldFilterTeachingGroupsClientSideForCompatibility = (): boolean => false;

/** Architecture guard: UI must never infer a TeachingGroup from SubjectAllocation uniqueness. */
export const shouldInferTeachingGroupFromSubjectAllocation = (): boolean => false;

/** Architecture guard: UI must never auto-assign or auto-clear TeachingGroupId. */
export const shouldSilentlyAssignOrClearTeachingGroup = (): boolean => false;

export const compatibleTeachingGroupsPath = (entryId: number): string =>
  `/scheduling/timetables/entries/${entryId}/compatible-teaching-groups`;

export const assignTeachingGroupPath = (entryId: number): string =>
  `/scheduling/timetables/entries/${entryId}/teaching-group`;

export const clearTeachingGroupPath = (entryId: number): string =>
  `/scheduling/timetables/entries/${entryId}/teaching-group`;
