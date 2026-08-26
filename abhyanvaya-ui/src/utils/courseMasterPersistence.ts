/**
 * AI29.1D.24 / P1-3 — Course Master save plan.
 * DepartmentId is always required. ProgramId is sent when EnablePrograms.
 * Server orchestrates assign-course; UI must never call POST /programs/assign-course separately.
 */

export type CourseMasterSavePlan = {
  mode: "create" | "update";
  coursePayload: {
    id?: number;
    code: string;
    name: string;
    departmentId: number;
    programId?: number | null;
  };
  /** Always false — server CourseController invokes AssignCourseToProgramAsync. */
  callAssignCourseSeparately: false;
};

export function buildCourseMasterSavePlan(input: {
  editingId: number;
  code: string;
  name: string;
  departmentId: number;
  programId: number;
  enablePrograms: boolean;
}): CourseMasterSavePlan {
  const code = input.code.trim().toUpperCase();
  const name = input.name.trim();
  const selectedProgramId =
    input.enablePrograms && input.programId > 0 ? input.programId : input.enablePrograms ? null : undefined;

  if (input.editingId > 0) {
    return {
      mode: "update",
      coursePayload: {
        id: input.editingId,
        code,
        name,
        departmentId: input.departmentId,
        ...(input.enablePrograms ? { programId: selectedProgramId ?? null } : {}),
      },
      callAssignCourseSeparately: false,
    };
  }

  return {
    mode: "create",
    coursePayload: {
      code,
      name,
      departmentId: input.departmentId,
      ...(input.enablePrograms ? { programId: selectedProgramId ?? null } : {}),
    },
    callAssignCourseSeparately: false,
  };
}
