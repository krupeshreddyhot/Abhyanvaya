import { describe, expect, it } from "vitest";
import {
  buildProgramReassignmentCopy,
  normalizeProgramId,
  shouldConfirmProgramReassignment,
} from "./programReassignmentConfirmation";

describe("shouldConfirmProgramReassignment — AI29.1D.24A Prompt 2 matrix", () => {
  it("New Course → no confirmation", () => {
    expect(
      shouldConfirmProgramReassignment({
        currentProgramId: null,
        requestedProgramId: 10,
        isExistingCourse: false,
        programsEnabled: true,
      }),
    ).toBe(false);
  });

  it("None → Commerce → no confirmation", () => {
    expect(
      shouldConfirmProgramReassignment({
        currentProgramId: null,
        requestedProgramId: 10,
        isExistingCourse: true,
        programsEnabled: true,
      }),
    ).toBe(false);
  });

  it("Commerce → Commerce → no confirmation", () => {
    expect(
      shouldConfirmProgramReassignment({
        currentProgramId: 10,
        requestedProgramId: 10,
        isExistingCourse: true,
        programsEnabled: true,
      }),
    ).toBe(false);
  });

  it("Commerce → Science → confirmation", () => {
    expect(
      shouldConfirmProgramReassignment({
        currentProgramId: 10,
        requestedProgramId: 20,
        isExistingCourse: true,
        programsEnabled: true,
      }),
    ).toBe(true);
  });

  it("Commerce → None → confirmation", () => {
    expect(
      shouldConfirmProgramReassignment({
        currentProgramId: 10,
        requestedProgramId: null,
        isExistingCourse: true,
        programsEnabled: true,
      }),
    ).toBe(true);
  });

  it("Science → Commerce → confirmation", () => {
    expect(
      shouldConfirmProgramReassignment({
        currentProgramId: 20,
        requestedProgramId: 10,
        isExistingCourse: true,
        programsEnabled: true,
      }),
    ).toBe(true);
  });

  it("Science → None → confirmation", () => {
    expect(
      shouldConfirmProgramReassignment({
        currentProgramId: 20,
        requestedProgramId: 0,
        isExistingCourse: true,
        programsEnabled: true,
      }),
    ).toBe(true);
  });

  it("None → None → no confirmation", () => {
    expect(
      shouldConfirmProgramReassignment({
        currentProgramId: null,
        requestedProgramId: null,
        isExistingCourse: true,
        programsEnabled: true,
      }),
    ).toBe(false);
  });

  it("Programs disabled → no confirmation", () => {
    expect(
      shouldConfirmProgramReassignment({
        currentProgramId: 10,
        requestedProgramId: 20,
        isExistingCourse: true,
        programsEnabled: false,
      }),
    ).toBe(false);
  });

  it("normalizeProgramId treats 0 as none", () => {
    expect(normalizeProgramId(0)).toBeNull();
    expect(normalizeProgramId(15)).toBe(15);
  });

  it("buildProgramReassignmentCopy describes move", () => {
    const copy = buildProgramReassignmentCopy({
      courseLabel: "B.Com",
      currentProgramName: "Commerce",
      requestedProgramName: "Science",
    });
    expect(copy.title).toBe("Change Course Program?");
    expect(copy.description).toContain("Commerce");
    expect(copy.description).toContain("Science");
    expect(copy.description).toContain("B.Com");
  });
});
