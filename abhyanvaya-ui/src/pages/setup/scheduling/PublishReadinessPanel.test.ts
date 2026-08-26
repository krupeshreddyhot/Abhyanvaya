import { describe, expect, it } from "vitest";
import {
  formatFindingContextLine,
  formatFindingMetricsLine,
  publishFindingCodeLabel,
} from "./publishReadinessPresentation";
import { getPublishBlockers } from "./publishReadiness";
import { TimetableStatus, type TimetablePublishReadinessResultDto } from "../../../services/schedulingService";

const readiness = (
  findings: TimetablePublishReadinessResultDto["findings"],
  isReady: boolean,
): TimetablePublishReadinessResultDto => ({
  timetableId: 1,
  lifecycleState: TimetableStatus.Locked,
  isFrozen: false,
  isReady,
  blockingFindingCount: findings.filter((f) => f.isBlocking).length,
  warningFindingCount: 0,
  informationalFindingCount: 0,
  evaluatedAtUtc: "2026-08-20T00:00:00Z",
  findings,
});

describe("AI-SCHED-CAP Prompt 8.3 — PublishReadinessPanel presentation helpers", () => {
  it("labels known codes and falls back for unknown codes", () => {
    expect(publishFindingCodeLabel("ROOM_CAPACITY")).toBe("Room capacity");
    expect(publishFindingCodeLabel("TEACHING_GROUP_CAPACITY_EXCEEDED")).toBe("Teaching Group capacity");
    expect(publishFindingCodeLabel("FUTURE_UNKNOWN_RULE")).toBe("Publish blocker");
  });

  it("formats context and metrics from server fields only", () => {
    const finding = {
      code: "ROOM_CAPACITY",
      severity: "Error",
      isBlocking: true,
      title: "Room capacity exceeded",
      why: "why",
      recommendedAction: "act",
      timetableEntryId: 9,
      dayOfWeek: 1,
      timeSlotId: 3,
      roomId: 4,
      placementSize: 40,
      effectiveRoomCapacity: 30,
    };
    expect(formatFindingContextLine(finding)).toContain("Entry #9");
    expect(formatFindingContextLine(finding)).toContain("Monday");
    expect(formatFindingMetricsLine(finding)).toContain("Placement: 40");
    expect(
      formatFindingContextLine({
        ...finding,
        timetableEntryId: null,
        dayOfWeek: null,
        timeSlotId: null,
        roomId: null,
      }),
    ).toBeNull();
  });

  it("blockers use server isBlocking only", () => {
    const result = readiness(
      [
        {
          code: "ROOM_WRONG_TYPE",
          severity: "Error",
          isBlocking: false,
          title: "Wrong type",
          why: "w",
          recommendedAction: "a",
        },
        {
          code: "ROOM_CAPACITY",
          severity: "Error",
          isBlocking: true,
          title: "Capacity",
          why: "w",
          recommendedAction: "a",
          timetableEntryId: 1,
        },
      ],
      false,
    );
    const blockers = getPublishBlockers(result);
    expect(blockers).toHaveLength(1);
    expect(blockers[0].code).toBe("ROOM_CAPACITY");
  });

  it("ready timetable has no blockers", () => {
    const result = readiness([], true);
    expect(getPublishBlockers(result)).toHaveLength(0);
    expect(result.isReady).toBe(true);
  });
});
