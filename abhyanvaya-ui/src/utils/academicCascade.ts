import type { AcademicHierarchyNodeDto } from "../services/programService";
import { filterSemestersForScope, type CourseRow, type GroupRow, type SemesterRow } from "../services/setupService";
import type { SectionDto } from "../services/sectionService";
import type { AcademicUiSelection } from "../types/academicUiContext";
import { emptyAcademicUiSelection } from "../types/academicUiContext";

export type CourseLike = Pick<CourseRow, "id"> & { programId?: number | null };
export type GroupLike = Pick<GroupRow, "id" | "courseId">;
export type SemesterLike = Pick<SemesterRow, "id" | "courseId" | "groupId">;
export type SectionLike = Pick<SectionDto, "id" | "academicYearId" | "courseId" | "groupId" | "semesterId">;

/**
 * Build programId → courseId set from GET /academic-structure hierarchy.
 * Program nodes contain Course children when EnablePrograms is true.
 */
export const buildProgramCourseIndex = (
  roots: AcademicHierarchyNodeDto[] | null | undefined,
): Map<number, Set<number>> => {
  const index = new Map<number, Set<number>>();
  if (!roots?.length) return index;

  const visit = (node: AcademicHierarchyNodeDto, programId: number | null) => {
    const kind = (node.kind ?? "").toLowerCase();
    let nextProgramId = programId;
    if (kind === "program") {
      nextProgramId = node.id;
      if (!index.has(node.id)) index.set(node.id, new Set());
    }
    if (kind === "course" && nextProgramId != null) {
      if (!index.has(nextProgramId)) index.set(nextProgramId, new Set());
      index.get(nextProgramId)!.add(node.id);
    }
    for (const child of node.children ?? []) {
      visit(child, nextProgramId);
    }
  };

  for (const root of roots) visit(root, null);
  return index;
};

export type ProgramCourseFilterOptions = {
  /**
   * Hierarchy GET succeeded (index may still be empty for a program).
   * Required for Program-mode course filtering when Programs are enabled.
   */
  hierarchyReady?: boolean;
  /**
   * Hierarchy GET failed — fail closed (no Course options).
   * Do not fall back to the full course catalog.
   */
  hierarchyFailed?: boolean;
};

/** Hierarchy projection disagreed with authoritative Course.ProgramId. */
export type HierarchyConsistencyWarning = {
  courseId: number;
  hierarchyProgramId: number;
  courseProgramId: number | null;
  message: string;
};

/**
 * Detect stale hierarchy projection vs authoritative Course.ProgramId.
 * Hierarchy must not override Course.ProgramId — warnings are diagnostic only.
 */
export const collectHierarchyConsistencyWarnings = (
  courses: CourseLike[],
  programCourseIndex?: Map<number, Set<number>> | null,
): HierarchyConsistencyWarning[] => {
  if (!programCourseIndex?.size) return [];
  const byId = new Map(courses.map((c) => [c.id, c]));
  const warnings: HierarchyConsistencyWarning[] = [];

  for (const [hierarchyProgramId, courseIds] of programCourseIndex) {
    for (const courseId of courseIds) {
      const course = byId.get(courseId);
      const courseProgramId = course?.programId ?? null;
      if (courseProgramId == null || Number(courseProgramId) !== Number(hierarchyProgramId)) {
        warnings.push({
          courseId,
          hierarchyProgramId: Number(hierarchyProgramId),
          courseProgramId: courseProgramId == null ? null : Number(courseProgramId),
          message:
            courseProgramId == null
              ? `Hierarchy lists course ${courseId} under program ${hierarchyProgramId}, but Course.ProgramId is null.`
              : `Hierarchy lists course ${courseId} under program ${hierarchyProgramId}, but Course.ProgramId is ${courseProgramId}.`,
        });
      }
    }
  }

  return warnings;
};

/**
 * Program → Course filter (AI29.1D Prompt 4B — feature-mode + ProgramId authority).
 *
 * - EnablePrograms = false → legacy: all authorized courses.
 * - EnablePrograms = true → Program mode (even if zero Programs configured):
 *   - no Program selected → empty
 *   - Program selected → only courses with Course.ProgramId === selected Program
 *   - null ProgramId → never shown
 *   - hierarchy membership alone never includes a course
 * - Hierarchy failed / not ready → empty (no catalog fallback).
 */
export const filterCoursesForProgram = <T extends CourseLike>(
  courses: T[],
  enablePrograms: boolean,
  programId: number | null,
  programCourseIndex?: Map<number, Set<number>> | null,
  options?: ProgramCourseFilterOptions,
): T[] => {
  if (!enablePrograms) return courses;

  // Fail closed while hierarchy is unavailable or failed.
  if (options?.hierarchyFailed) return [];
  if (!options?.hierarchyReady) return [];

  // Program mode: require an explicit Program selection (no full-catalog fallback).
  if (programId == null) return [];

  const pid = Number(programId);
  // Course.ProgramId is authoritative. Hierarchy is not used for inclusion.
  void programCourseIndex;
  return courses.filter((c) => c.programId != null && Number(c.programId) === pid);
};

export const filterGroupsForCourse = <T extends GroupLike>(groups: T[], courseId: number | null): T[] => {
  if (courseId == null) return [];
  return groups.filter((g) => Number(g.courseId) === Number(courseId));
};

export const filterSemestersForCourseGroup = (
  semesters: SemesterRow[],
  courseId: number | null,
  groupId: number | null,
): SemesterRow[] => {
  if (courseId == null) return [];
  if (groupId == null) {
    // Without Group: Group-specific rows only (historical NULL-group excluded).
    return semesters.filter((s) => Number(s.courseId) === Number(courseId) && s.groupId != null);
  }
  return filterSemestersForScope(semesters, courseId, groupId);
};

export const filterSectionsForScope = <T extends SectionLike>(
  sections: T[],
  scope: {
    academicYearId?: number | null;
    courseId?: number | null;
    groupId?: number | null;
    semesterId?: number | null;
  },
): T[] => {
  const { academicYearId, courseId, groupId, semesterId } = scope;
  return sections.filter((s) => {
    if (academicYearId != null && Number(s.academicYearId) !== Number(academicYearId)) return false;
    if (courseId != null && Number(s.courseId) !== Number(courseId)) return false;
    if (groupId != null && Number(s.groupId) !== Number(groupId)) return false;
    if (semesterId != null && Number(s.semesterId) !== Number(semesterId)) return false;
    return true;
  });
};

/** Levels cleared when a parent selection changes (Section never clears Subject). */
const CLEAR_BELOW: Record<string, (keyof AcademicUiSelection)[]> = {
  academicYear: ["sectionId", "sectionIds"],
  program: ["courseId", "groupId", "semesterId", "sectionId", "sectionIds", "subjectId"],
  course: ["groupId", "semesterId", "sectionId", "sectionIds", "subjectId"],
  group: ["semesterId", "sectionId", "sectionIds", "subjectId"],
  semester: ["sectionId", "sectionIds", "subjectId"],
  section: [], // subject/faculty untouched
  subject: [],
  faculty: [],
};

export type CascadePatch = Partial<AcademicUiSelection>;

/**
 * Apply a selection change with cascade clears.
 * Changing Section does not clear Subject (Subject Master = Course + Group + Semester).
 */
export const applyCascadeSelection = (
  current: AcademicUiSelection,
  patch: CascadePatch,
): AcademicUiSelection => {
  const next: AcademicUiSelection = { ...current, sectionIds: [...current.sectionIds] };

  const applyClears = (level: keyof typeof CLEAR_BELOW) => {
    for (const key of CLEAR_BELOW[level]) {
      if (key === "sectionIds") next.sectionIds = [];
      else if (key === "courseId") next.courseId = null;
      else if (key === "groupId") next.groupId = null;
      else if (key === "semesterId") next.semesterId = null;
      else if (key === "sectionId") next.sectionId = null;
      else if (key === "subjectId") next.subjectId = null;
      else if (key === "programId") next.programId = null;
      else if (key === "facultyId") next.facultyId = null;
      else if (key === "academicYearId") next.academicYearId = null;
    }
  };

  if ("academicYearId" in patch && patch.academicYearId !== current.academicYearId) {
    applyClears("academicYear");
    next.academicYearId = patch.academicYearId ?? null;
  }
  if ("programId" in patch && patch.programId !== current.programId) {
    applyClears("program");
    next.programId = patch.programId ?? null;
  }
  if ("courseId" in patch && patch.courseId !== current.courseId) {
    applyClears("course");
    next.courseId = patch.courseId ?? null;
  }
  if ("groupId" in patch && patch.groupId !== current.groupId) {
    applyClears("group");
    next.groupId = patch.groupId ?? null;
  }
  if ("semesterId" in patch && patch.semesterId !== current.semesterId) {
    applyClears("semester");
    next.semesterId = patch.semesterId ?? null;
  }
  if ("sectionId" in patch && patch.sectionId !== current.sectionId) {
    applyClears("section");
    next.sectionId = patch.sectionId ?? null;
    if (patch.sectionId != null) {
      next.sectionIds = [patch.sectionId];
    } else if (!("sectionIds" in patch)) {
      next.sectionIds = [];
    }
  }
  if ("sectionIds" in patch && patch.sectionIds) {
    next.sectionIds = [...patch.sectionIds];
    next.sectionId = patch.sectionIds.length === 1 ? patch.sectionIds[0]! : null;
  }
  if ("subjectId" in patch) {
    next.subjectId = patch.subjectId ?? null;
  }
  if ("facultyId" in patch) {
    next.facultyId = patch.facultyId ?? null;
  }

  return next;
};

/**
 * Drop invalid child IDs that are no longer in filtered option lists
 * (e.g. after Program/Course change or Programs disabled).
 */
export const sanitizeSelectionAgainstOptions = (
  selection: AcademicUiSelection,
  options: {
    enablePrograms: boolean;
    programIds: Set<number>;
    courseIds: Set<number>;
    groupIds: Set<number>;
    semesterIds: Set<number>;
    sectionIds: Set<number>;
    subjectIds: Set<number>;
  },
): AcademicUiSelection => {
  let next = { ...selection, sectionIds: [...selection.sectionIds] };

  if (!options.enablePrograms) {
    next.programId = null;
  } else if (next.programId != null && !options.programIds.has(next.programId)) {
    next = applyCascadeSelection(next, { programId: null });
  }

  if (next.courseId != null && !options.courseIds.has(next.courseId)) {
    next = applyCascadeSelection(next, { courseId: null });
  }
  if (next.groupId != null && !options.groupIds.has(next.groupId)) {
    next = applyCascadeSelection(next, { groupId: null });
  }
  if (next.semesterId != null && !options.semesterIds.has(next.semesterId)) {
    next = applyCascadeSelection(next, { semesterId: null });
  }
  if (next.sectionId != null && !options.sectionIds.has(next.sectionId)) {
    next = applyCascadeSelection(next, { sectionId: null });
  }
  // Drop ids not in the current option list. Do NOT re-inject sectionId when options are empty —
  // that previously oscillated [] ↔ [id] and could loop with context option rebuilds.
  next.sectionIds = next.sectionIds.filter((id) => options.sectionIds.has(id));
  if (next.sectionId != null && options.sectionIds.has(next.sectionId) && !next.sectionIds.includes(next.sectionId)) {
    next.sectionIds = [next.sectionId];
  }
  if (next.subjectId != null && options.subjectIds.size > 0 && !options.subjectIds.has(next.subjectId)) {
    next.subjectId = null;
  }

  return next;
};

export const resetAcademicSelection = (): AcademicUiSelection => emptyAcademicUiSelection();

/** Cascade path labels for UI documentation / breadcrumbs. */
export const academicCascadePath = (enablePrograms: boolean): string =>
  enablePrograms
    ? "Program → Course → Group → Semester → Section (optional) · Subject via Course + Group + Semester"
    : "Course → Group → Semester → Section (optional) · Subject via Course + Group + Semester";
