import type { AcademicUiSelection } from "../types/academicUiContext";

export type AcademicSelectorField =
  | "academicYear"
  | "program"
  | "course"
  | "group"
  | "semester"
  | "section"
  | "subject";

export const DEFAULT_ACADEMIC_SELECTOR_FIELDS: AcademicSelectorField[] = [
  "academicYear",
  "program",
  "course",
  "group",
  "semester",
  "section",
  "subject",
];

export type AcademicSelectorFieldState = {
  field: AcademicSelectorField;
  visible: boolean;
  disabled: boolean;
  loading: boolean;
  empty: boolean;
  /** Why the control is disabled / empty (for helper text). */
  helperText: string | null;
};

export type AcademicSelectorFieldStateInput = {
  field: AcademicSelectorField;
  /** Tenant EnablePrograms flag. */
  enablePrograms: boolean;
  /** Programs enabled and at least one program exists. */
  programsAvailable: boolean;
  selection: AcademicUiSelection;
  optionCount: number;
  catalogLoading: boolean;
  sectionsLoading?: boolean;
  subjectsLoading?: boolean;
  /** Force-disable entire selector. */
  forceDisabled?: boolean;
  /**
   * When true, the field may be cleared (optional).
   * Section defaults to optional; Subject Master never uses Section.
   */
  allowEmpty?: boolean;
  /** Academic hierarchy failed while Programs are enabled — Course options fail closed. */
  hierarchyFailed?: boolean;
};

/**
 * Pure field visibility / enablement for AcademicScopeSelector.
 * Subject depends on Course + Group + Semester only — never Section.
 */
export const resolveAcademicSelectorFieldState = (
  input: AcademicSelectorFieldStateInput,
): AcademicSelectorFieldState => {
  const {
    field,
    enablePrograms,
    programsAvailable,
    selection,
    optionCount,
    catalogLoading,
    sectionsLoading = false,
    subjectsLoading = false,
    forceDisabled = false,
    hierarchyFailed = false,
  } = input;

  const base = (partial: Omit<AcademicSelectorFieldState, "field">): AcademicSelectorFieldState => ({
    field,
    ...partial,
  });

  if (field === "program") {
    const visible = enablePrograms;
    if (!visible) {
      return base({ visible: false, disabled: true, loading: false, empty: true, helperText: null });
    }
    const disabled = forceDisabled || catalogLoading;
    const empty = optionCount === 0;
    return base({
      visible: true,
      disabled,
      loading: catalogLoading,
      empty,
      helperText: empty && !catalogLoading ? "No academic programs have been configured." : null,
    });
  }

  if (field === "academicYear") {
    const disabled = forceDisabled || catalogLoading;
    const empty = optionCount === 0;
    return base({
      visible: true,
      disabled,
      loading: catalogLoading,
      empty,
      helperText: empty && !catalogLoading ? "No academic years available." : null,
    });
  }

  if (field === "course") {
    // Prompt 4B: EnablePrograms alone activates Program mode (even with zero Programs configured).
    const programMode = enablePrograms;
    const noProgramsConfigured = programMode && !programsAvailable;
    const waitingOnProgram = programMode && selection.programId == null;
    const hierarchyBlocks = programMode && hierarchyFailed;
    const disabled =
      forceDisabled || catalogLoading || waitingOnProgram || hierarchyBlocks || noProgramsConfigured;
    const empty = !waitingOnProgram && !hierarchyBlocks && !noProgramsConfigured && optionCount === 0;
    let helperText: string | null = null;
    if (hierarchyBlocks && !catalogLoading) {
      helperText = "Academic hierarchy could not be loaded. Refresh catalogs to retry.";
    } else if (noProgramsConfigured && !catalogLoading) {
      helperText = "No academic programs have been configured.";
    } else if (waitingOnProgram) {
      helperText = "Select a Program first.";
    } else if (empty && !catalogLoading && programMode && selection.programId != null) {
      helperText = "No courses are assigned to this program.";
    } else if (empty && !catalogLoading) {
      helperText = "No courses available.";
    }
    return base({
      visible: true,
      disabled,
      loading: catalogLoading,
      empty: empty || hierarchyBlocks || noProgramsConfigured,
      helperText,
    });
  }

  if (field === "group") {
    const waiting = selection.courseId == null;
    const disabled = forceDisabled || catalogLoading || waiting;
    const empty = !waiting && optionCount === 0;
    return base({
      visible: true,
      disabled,
      loading: catalogLoading,
      empty,
      helperText: waiting
        ? "Select a Course first."
        : empty && !catalogLoading
          ? "No groups for the selected Course."
          : null,
    });
  }

  if (field === "semester") {
    const waiting = selection.courseId == null || selection.groupId == null;
    const disabled = forceDisabled || catalogLoading || waiting;
    const empty = !waiting && optionCount === 0;
    return base({
      visible: true,
      disabled,
      loading: catalogLoading,
      empty,
      helperText: waiting
        ? "Select Course and Group first."
        : empty && !catalogLoading
          ? "No semesters for the selected Course and Group. In Semesters setup, add a Group-specific Semester for this Group."
          : null,
    });
  }

  if (field === "section") {
    // Operational: Year + Course + Group + Semester. Not a Subject Master dimension.
    const waiting =
      selection.academicYearId == null ||
      selection.courseId == null ||
      selection.groupId == null ||
      selection.semesterId == null;
    const disabled = forceDisabled || catalogLoading || waiting || sectionsLoading;
    const empty = !waiting && !sectionsLoading && optionCount === 0;
    return base({
      visible: true,
      disabled,
      loading: sectionsLoading,
      empty,
      helperText: waiting
        ? "Select Academic Year, Course, Group, and Semester first."
        : empty
          ? "No sections for this scope."
          : null,
    });
  }

  // subject — Course + Group + Semester only (never Section)
  const waiting = selection.courseId == null || selection.groupId == null || selection.semesterId == null;
  const disabled = forceDisabled || catalogLoading || waiting || subjectsLoading;
  const empty = !waiting && !subjectsLoading && optionCount === 0;
  return base({
    visible: true,
    disabled,
    loading: subjectsLoading,
    empty,
    helperText: waiting
      ? "Select Course, Group, and Semester first."
      : empty
        ? "No subjects for Course + Group + Semester."
        : null,
  });
};

export const isAcademicScopeReady = (
  selection: AcademicUiSelection,
  opts?: { requireProgram?: boolean; requireSection?: boolean; requireSubject?: boolean },
): boolean => {
  if (opts?.requireProgram && selection.programId == null) return false;
  if (selection.academicYearId == null) return false;
  if (selection.courseId == null || selection.groupId == null || selection.semesterId == null) return false;
  if (opts?.requireSection && selection.sectionId == null && selection.sectionIds.length === 0) return false;
  if (opts?.requireSubject && selection.subjectId == null) return false;
  return true;
};
