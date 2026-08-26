import { describe, expect, it } from "vitest";
import { emptyAcademicUiSelection } from "../types/academicUiContext";
import {
  isAcademicScopeReady,
  resolveAcademicSelectorFieldState,
} from "./academicSelectorFieldState";

const base = {
  catalogLoading: false,
  optionCount: 3,
  forceDisabled: false,
};

describe("resolveAcademicSelectorFieldState", () => {
  it("hides Program when Programs are disabled", () => {
    const state = resolveAcademicSelectorFieldState({
      ...base,
      field: "program",
      enablePrograms: false,
      programsAvailable: false,
      selection: emptyAcademicUiSelection(),
    });
    expect(state.visible).toBe(false);
  });

  it("shows Program when Programs are enabled", () => {
    const state = resolveAcademicSelectorFieldState({
      ...base,
      field: "program",
      enablePrograms: true,
      programsAvailable: true,
      selection: emptyAcademicUiSelection(),
    });
    expect(state.visible).toBe(true);
    expect(state.disabled).toBe(false);
  });

  it("disables Course until Program is selected when Programs enabled", () => {
    const waiting = resolveAcademicSelectorFieldState({
      ...base,
      field: "course",
      enablePrograms: true,
      programsAvailable: true,
      selection: emptyAcademicUiSelection(),
    });
    expect(waiting.disabled).toBe(true);
    expect(waiting.helperText).toMatch(/Program/i);

    const ready = resolveAcademicSelectorFieldState({
      ...base,
      field: "course",
      enablePrograms: true,
      programsAvailable: true,
      selection: { ...emptyAcademicUiSelection(), programId: 1 },
    });
    expect(ready.disabled).toBe(false);
  });

  it("Programs enabled but zero Programs configured → empty Program + disabled Course", () => {
    const program = resolveAcademicSelectorFieldState({
      ...base,
      field: "program",
      enablePrograms: true,
      programsAvailable: false,
      selection: emptyAcademicUiSelection(),
      optionCount: 0,
    });
    expect(program.visible).toBe(true);
    expect(program.empty).toBe(true);
    expect(program.helperText).toBe("No academic programs have been configured.");

    const course = resolveAcademicSelectorFieldState({
      ...base,
      field: "course",
      enablePrograms: true,
      programsAvailable: false,
      selection: emptyAcademicUiSelection(),
      optionCount: 3,
    });
    expect(course.disabled).toBe(true);
    expect(course.empty).toBe(true);
    expect(course.helperText).toBe("No academic programs have been configured.");
  });

  it("shows fail-closed empty message when Program has zero Courses", () => {
    const state = resolveAcademicSelectorFieldState({
      ...base,
      field: "course",
      enablePrograms: true,
      programsAvailable: true,
      selection: { ...emptyAcademicUiSelection(), programId: 1 },
      optionCount: 0,
    });
    expect(state.empty).toBe(true);
    expect(state.helperText).toBe("No courses are assigned to this program.");
  });

  it("blocks Course options when hierarchy failed", () => {
    const state = resolveAcademicSelectorFieldState({
      ...base,
      field: "course",
      enablePrograms: true,
      programsAvailable: true,
      selection: { ...emptyAcademicUiSelection(), programId: 1 },
      optionCount: 0,
      hierarchyFailed: true,
    });
    expect(state.disabled).toBe(true);
    expect(state.helperText).toMatch(/hierarchy/i);
  });

  it("does not require Program for Course when Programs disabled (legacy)", () => {
    const state = resolveAcademicSelectorFieldState({
      ...base,
      field: "course",
      enablePrograms: false,
      programsAvailable: false,
      selection: emptyAcademicUiSelection(),
    });
    expect(state.disabled).toBe(false);
  });

  it("cascades Group ← Course and Semester ← Course+Group", () => {
    const group = resolveAcademicSelectorFieldState({
      ...base,
      field: "group",
      enablePrograms: false,
      programsAvailable: false,
      selection: emptyAcademicUiSelection(),
    });
    expect(group.disabled).toBe(true);

    const semester = resolveAcademicSelectorFieldState({
      ...base,
      field: "semester",
      enablePrograms: false,
      programsAvailable: false,
      selection: { ...emptyAcademicUiSelection(), courseId: 1 },
    });
    expect(semester.disabled).toBe(true);
    expect(semester.helperText).toMatch(/Group/i);
  });

  it("enables Section only after Year + Course + Group + Semester", () => {
    const partial = resolveAcademicSelectorFieldState({
      ...base,
      field: "section",
      enablePrograms: false,
      programsAvailable: false,
      selection: {
        ...emptyAcademicUiSelection(),
        courseId: 1,
        groupId: 2,
        semesterId: 3,
      },
    });
    expect(partial.disabled).toBe(true);

    const ready = resolveAcademicSelectorFieldState({
      ...base,
      field: "section",
      enablePrograms: false,
      programsAvailable: false,
      selection: {
        ...emptyAcademicUiSelection(),
        academicYearId: 9,
        courseId: 1,
        groupId: 2,
        semesterId: 3,
      },
    });
    expect(ready.disabled).toBe(false);
  });

  it("enables Subject from Course + Group + Semester without requiring Section", () => {
    const withSectionMissing = resolveAcademicSelectorFieldState({
      ...base,
      field: "subject",
      enablePrograms: false,
      programsAvailable: false,
      selection: {
        ...emptyAcademicUiSelection(),
        courseId: 1,
        groupId: 2,
        semesterId: 3,
        sectionId: null,
      },
    });
    expect(withSectionMissing.disabled).toBe(false);
    expect(withSectionMissing.helperText).toBeNull();
  });

  it("exposes loading and empty states for Subject", () => {
    const loading = resolveAcademicSelectorFieldState({
      ...base,
      field: "subject",
      enablePrograms: false,
      programsAvailable: false,
      selection: {
        ...emptyAcademicUiSelection(),
        courseId: 1,
        groupId: 2,
        semesterId: 3,
      },
      optionCount: 0,
      subjectsLoading: true,
    });
    expect(loading.loading).toBe(true);
    expect(loading.empty).toBe(false);

    const empty = resolveAcademicSelectorFieldState({
      ...base,
      field: "subject",
      enablePrograms: false,
      programsAvailable: false,
      selection: {
        ...emptyAcademicUiSelection(),
        courseId: 1,
        groupId: 2,
        semesterId: 3,
      },
      optionCount: 0,
      subjectsLoading: false,
    });
    expect(empty.empty).toBe(true);
    expect(empty.helperText).toMatch(/No subjects/i);
  });
});

describe("isAcademicScopeReady", () => {
  it("requires Year + Course + Group + Semester by default", () => {
    expect(
      isAcademicScopeReady({
        ...emptyAcademicUiSelection(),
        academicYearId: 1,
        courseId: 2,
        groupId: 3,
        semesterId: 4,
      }),
    ).toBe(true);
    expect(
      isAcademicScopeReady({
        ...emptyAcademicUiSelection(),
        courseId: 2,
        groupId: 3,
        semesterId: 4,
      }),
    ).toBe(false);
  });

  it("can optionally require Subject without requiring Section", () => {
    expect(
      isAcademicScopeReady(
        {
          ...emptyAcademicUiSelection(),
          academicYearId: 1,
          courseId: 2,
          groupId: 3,
          semesterId: 4,
          subjectId: 9,
        },
        { requireSubject: true },
      ),
    ).toBe(true);
    expect(
      isAcademicScopeReady(
        {
          ...emptyAcademicUiSelection(),
          academicYearId: 1,
          courseId: 2,
          groupId: 3,
          semesterId: 4,
        },
        { requireSubject: true },
      ),
    ).toBe(false);
  });
});
