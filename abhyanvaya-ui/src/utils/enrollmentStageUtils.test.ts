import { describe, expect, it } from "vitest";
import { BatchProgressState, type BatchProgressDto } from "../types/enrollment";
import {
  ENROLLMENT_STAGES,
  buildTimelineEvents,
  getCurrentStageIndex,
  getStagePercentage,
} from "./enrollmentStageUtils";

describe("enrollmentStageUtils", () => {
  const progress: BatchProgressDto = {
    batchId: "b1",
    state: BatchProgressState.Downloading,
    percentage: 25,
    estimatedRemaining: "PT5M",
    queued: 10,
    downloading: 5,
    validating: 0,
    embedding: 0,
    completed: 0,
    failed: 0,
    cancelled: 0,
  };

  it("resolves current stage index from pipeline state", () => {
    expect(getCurrentStageIndex(BatchProgressState.Downloading)).toBe(0);
    expect(getStagePercentage(5, 20)).toBe(25);
  });

  it("defines five presentation stages", () => {
    expect(ENROLLMENT_STAGES.map((s) => s.label)).toContain("Downloading Photos");
    expect(ENROLLMENT_STAGES.map((s) => s.label)).toContain("Completed");
  });

  it("builds timeline events from batch metadata", () => {
    const events = buildTimelineEvents(progress, {
      createdUtc: "2026-07-18T10:00:00Z",
      startedUtc: "2026-07-18T10:01:00Z",
      completedUtc: null,
      totalStudents: 20,
      failedCount: 0,
    });
    expect(events[0]?.title).toBe("Batch Created");
    expect(events.some((e) => e.title === "Download Started")).toBe(true);
  });
});
