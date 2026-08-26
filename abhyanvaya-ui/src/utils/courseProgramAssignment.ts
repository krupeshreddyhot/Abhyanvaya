import type { ProgramDto } from "../services/programService";

/** Active non-archived programs for new Course→Program assignment. */
export function isAssignableProgram(p: Pick<ProgramDto, "isActive" | "status">): boolean {
  return p.isActive && String(p.status).toLowerCase() !== "archived";
}

/**
 * Programs shown in Course Master selector.
 * New assignment: Active only. Edit: keep current assignment visible even if Inactive.
 * Archived programs are never offered for *new* selection; existing Archived link still displays.
 */
export function programsForCourseAssignmentSelector(
  all: readonly ProgramDto[],
  currentProgramId: number | null,
): ProgramDto[] {
  const active = all.filter(isAssignableProgram);
  if (currentProgramId == null || currentProgramId <= 0) return active;
  if (active.some((p) => p.id === currentProgramId)) return active;
  const current = all.find((p) => p.id === currentProgramId);
  return current ? [...active, current] : active;
}
