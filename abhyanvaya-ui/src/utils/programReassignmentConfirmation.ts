/**
 * AI29.1D.24A — pure UI decision: whether to show Program reassignment confirmation.
 * No API calls. No persistence.
 */

export type ProgramReassignmentDecisionInput = {
  currentProgramId: number | null | undefined;
  requestedProgramId: number | null | undefined;
  isExistingCourse: boolean;
  programsEnabled: boolean;
};

/** Normalize UI/API Program ids: null/undefined/≤0 ⇒ none. */
export function normalizeProgramId(programId: number | null | undefined): number | null {
  return programId != null && programId > 0 ? programId : null;
}

/**
 * Returns true only when an existing Course would change from one Program
 * (including leaving a Program for None). First assignment from None does not confirm.
 */
export function shouldConfirmProgramReassignment(
  input: ProgramReassignmentDecisionInput,
): boolean {
  if (!input.programsEnabled) return false;
  if (!input.isExistingCourse) return false;

  const current = normalizeProgramId(input.currentProgramId);
  const requested = normalizeProgramId(input.requestedProgramId);

  if (current === requested) return false;

  // None → Program (first link): no confirmation.
  if (current == null && requested != null) return false;

  // Program → other Program, or Program → None.
  if (current != null && current !== requested) return true;

  return false;
}

export type ProgramReassignmentCopy = {
  title: string;
  description: string;
};

export function buildProgramReassignmentCopy(input: {
  courseLabel: string;
  currentProgramName: string;
  requestedProgramName: string;
}): ProgramReassignmentCopy {
  return {
    title: "Change Course Program?",
    description: `${input.courseLabel} is currently assigned to ${input.currentProgramName}. Changing it to ${input.requestedProgramName} will move the Course from ${input.currentProgramName} to ${input.requestedProgramName}.`,
  };
}
