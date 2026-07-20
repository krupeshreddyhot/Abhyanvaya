import { describe, expect, it } from "vitest";
import {
  WIZARD_STEPS,
  buildScopeFilters,
  describeEnrollmentScope,
  estimateProcessingSeconds,
  formatEstimatedDuration,
  parseIsoDurationSeconds,
} from "./enrollmentWizardUtils";

describe("enrollmentWizardUtils", () => {
  it("defines four context-driven wizard steps", () => {
    expect(WIZARD_STEPS).toEqual(["Academic Year", "Enrollment Scope", "Preview", "Confirm"]);
  });

  it("builds scope filters without exposing ids in labels", () => {
    const filters = buildScopeFilters(10, 2026, {
      courseId: 1,
      groupId: "",
      semesterId: "",
      batch: 2,
    });
    expect(filters).toMatchObject({ collegeId: 10, academicYear: 2026, courseId: 1, batch: 2 });
    expect(describeEnrollmentScope({ courseId: 1, groupId: "", semesterId: "", batch: 2 }, { course: "B.Tech" })).toContain(
      "B.Tech",
    );
  });

  it("parses ISO duration and estimates processing time", () => {
    expect(parseIsoDurationSeconds("PT30S")).toBe(30);
    const seconds = estimateProcessingSeconds(100, "PT30S", 4);
    expect(seconds).toBe(Math.ceil((100 * 30) / 4));
    expect(formatEstimatedDuration(seconds)).toContain("minute");
  });
});
