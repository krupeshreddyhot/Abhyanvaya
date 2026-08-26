/**
 * AI29.1D.24 Prompts 5–7 — Program Master course assignment helpers.
 * Authoritative relationship remains Course.ProgramId (same as Course Master / assign-course).
 */

export type CourseProgramRow = {
  id: number;
  code: string;
  name: string;
  programId?: number | null;
};

/** Courses not already on this Program (unassigned or linked elsewhere — assign moves them). */
export function coursesAvailableForProgramAssignment(
  allCourses: readonly CourseProgramRow[],
  programId: number,
): CourseProgramRow[] {
  if (programId <= 0) return [];
  return allCourses
    .filter((c) => (c.programId ?? null) !== programId)
    .slice()
    .sort((a, b) => a.code.localeCompare(b.code) || a.name.localeCompare(b.name));
}

/** Count derived from Course.ProgramId === programId (authoritative; not a manual counter). */
export function countCoursesForProgram(
  allCourses: readonly CourseProgramRow[],
  programId: number,
): number {
  if (programId <= 0) return 0;
  return allCourses.filter((c) => c.programId === programId).length;
}
