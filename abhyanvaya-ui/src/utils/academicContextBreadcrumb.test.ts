import { describe, expect, it } from "vitest";
import { emptyAcademicUiSelection } from "../types/academicUiContext";
import {
  academicContextQueryKey,
  toAcademicContextBreadcrumbQuery,
} from "./academicContextBreadcrumb";
import { hasAcademicContextSelection } from "../services/academicBreadcrumbService";

describe("AI29.1D Prompt 16 — academic context breadcrumb query mapping", () => {
  it("maps AcademicUi selection to API query without inventing labels", () => {
    const q = toAcademicContextBreadcrumbQuery({
      ...emptyAcademicUiSelection(),
      programId: 1,
      courseId: 2,
      groupId: 3,
      semesterId: 4,
      sectionId: 5,
      subjectId: 6,
    });
    expect(q).toMatchObject({
      programId: 1,
      courseId: 2,
      groupId: 3,
      semesterId: 4,
      sectionId: 5,
      subjectId: 6,
    });
    expect(hasAcademicContextSelection(q)).toBe(true);
  });

  it("merges faculty/current-class override over selection", () => {
    const q = toAcademicContextBreadcrumbQuery(
      { ...emptyAcademicUiSelection(), courseId: 9, groupId: 9 },
      { courseId: 2, groupId: 3, semesterId: 4, subjectId: 6 },
    );
    expect(q.courseId).toBe(2);
    expect(q.groupId).toBe(3);
    expect(q.semesterId).toBe(4);
    expect(q.subjectId).toBe(6);
  });

  it("empty selection is not fetchable", () => {
    expect(hasAcademicContextSelection(toAcademicContextBreadcrumbQuery(emptyAcademicUiSelection()))).toBe(
      false,
    );
  });

  it("query key is stable for sectionIds order", () => {
    const a = academicContextQueryKey({ sectionIds: [7, 5], courseId: 2 });
    const b = academicContextQueryKey({ sectionIds: [5, 7], courseId: 2 });
    expect(a).toBe(b);
  });
});
