import { describe, expect, it } from "vitest";
import { AxiosError, AxiosHeaders } from "axios";
import {
  formatPublishReadinessSummary,
  getPublishBlockers,
  getPublishNonBlockingFindings,
  isPublishReadinessError,
  normalizePublishReadiness,
  normalizePublishReadinessFinding,
  parsePublishFailure,
  PUBLISH_READINESS_CODES,
} from "./publishReadiness";
import { TimetableStatus, type TimetablePublishReadinessResultDto } from "../../../services/schedulingService";

const sampleReadiness = (
  overrides?: Partial<TimetablePublishReadinessResultDto>,
): TimetablePublishReadinessResultDto => ({
  timetableId: 50,
  lifecycleState: TimetableStatus.Locked,
  isFrozen: false,
  isReady: false,
  blockingFindingCount: 1,
  warningFindingCount: 0,
  informationalFindingCount: 0,
  evaluatedAtUtc: "2026-08-20T12:00:00Z",
  findings: [
    {
      code: PUBLISH_READINESS_CODES.RoomCapacity,
      severity: "Error",
      isBlocking: true,
      title: "Room capacity exceeded",
      why: "Placement exceeds effective room capacity.",
      recommendedAction: "Move to a larger room.",
      timetableEntryId: 9,
      timeSlotId: 3,
      roomId: 4,
      placementSize: 40,
      effectiveRoomCapacity: 30,
    },
  ],
  ...overrides,
});

const axiosError = (data: unknown, status = 400) => {
  const err = new AxiosError("Request failed");
  err.response = {
    status,
    data,
    statusText: "Bad Request",
    headers: {},
    config: { headers: new AxiosHeaders() },
  };
  return err;
};

describe("AI-SCHED-CAP Prompt 8.2 — publish readiness client contract", () => {
  it("normalizes backend readiness DTO property names (isReady / isBlocking / findings)", () => {
    const normalized = normalizePublishReadiness({
      timetableId: 1,
      lifecycleState: 2,
      isFrozen: false,
      isReady: false,
      blockingFindingCount: 1,
      warningFindingCount: 0,
      informationalFindingCount: 0,
      evaluatedAtUtc: "2026-08-20T00:00:00Z",
      findings: [
        {
          code: "TEACHING_GROUP_CAPACITY_EXCEEDED",
          severity: "Error",
          isBlocking: true,
          title: "TG capacity",
          why: "Resolved > Max",
          recommendedAction: "Reduce membership",
          timetableEntryId: 12,
          teachingGroupId: 7,
          resolvedStudentCount: 55,
          maxTeachingCapacity: 40,
        },
      ],
    });

    expect(normalized).not.toBeNull();
    expect(normalized!.isReady).toBe(false);
    expect(normalized!.findings[0].isBlocking).toBe(true);
    expect(normalized!.findings[0].code).toBe("TEACHING_GROUP_CAPACITY_EXCEEDED");
    expect(normalized!.findings[0].timetableEntryId).toBe(12);
    expect(normalized!.findings[0].recommendedAction).toBe("Reduce membership");
  });

  it("preserves server isBlocking and does not infer from severity", () => {
    const finding = normalizePublishReadinessFinding({
      code: "ROOM_WRONG_TYPE",
      severity: "Error",
      isBlocking: false,
      title: "Wrong type",
      why: "w",
      recommendedAction: "a",
    });
    expect(finding?.isBlocking).toBe(false);
    expect(finding?.severity).toBe("Error");

    const readiness = normalizePublishReadiness({
      timetableId: 1,
      isReady: true,
      findings: [
        {
          code: "ROOM_WRONG_TYPE",
          severity: "Error",
          isBlocking: false,
          title: "Wrong type",
          why: "w",
          recommendedAction: "a",
        },
      ],
    });
    expect(getPublishBlockers(readiness)).toHaveLength(0);
    expect(getPublishNonBlockingFindings(readiness)).toHaveLength(1);
  });

  it("preserves ROOM_CAPACITY and TEACHING_GROUP_CAPACITY_EXCEEDED codes", () => {
    const room = sampleReadiness();
    expect(getPublishBlockers(room)[0].code).toBe(PUBLISH_READINESS_CODES.RoomCapacity);

    const tg = sampleReadiness({
      findings: [
        {
          code: PUBLISH_READINESS_CODES.TeachingGroupCapacityExceeded,
          severity: "Error",
          isBlocking: true,
          title: "TG",
          why: "w",
          recommendedAction: "a",
          timetableEntryId: 1,
        },
      ],
    });
    expect(getPublishBlockers(tg)[0].code).toBe(
      PUBLISH_READINESS_CODES.TeachingGroupCapacityExceeded,
    );
  });

  it("handles unknown finding codes safely", () => {
    const readiness = normalizePublishReadiness({
      timetableId: 2,
      isReady: false,
      findings: [
        {
          code: "FUTURE_UNKNOWN_RULE",
          severity: "Critical",
          isBlocking: true,
          title: "Future rule",
          why: "Because",
          recommendedAction: "Fix it",
          timetableEntryId: 99,
        },
      ],
    });
    expect(readiness).not.toBeNull();
    const blocker = getPublishBlockers(readiness)[0];
    expect(blocker.code).toBe("FUTURE_UNKNOWN_RULE");
    expect(blocker.title).toBe("Future rule");
    expect(blocker.why).toBe("Because");
    expect(blocker.recommendedAction).toBe("Fix it");
    expect(blocker.timetableEntryId).toBe(99);
    expect(formatPublishReadinessSummary(readiness!)).toContain("Future rule");
  });

  it("preserves timetableEntryId metadata", () => {
    const finding = normalizePublishReadinessFinding({
      code: "ROOM_CAPACITY",
      severity: "Error",
      isBlocking: true,
      title: "t",
      why: "w",
      recommendedAction: "a",
      timetableEntryId: 4242,
    });
    expect(finding?.timetableEntryId).toBe(4242);
  });

  it("parsePublishFailure recognizes structured readiness 400 body", () => {
    const err = axiosError(sampleReadiness());
    const parsed = parsePublishFailure(err);
    expect(parsed.kind).toBe("readiness");
    expect(parsed.readiness?.findings[0].code).toBe("ROOM_CAPACITY");
    expect(isPublishReadinessError(err)).toBe(true);
  });

  it("parsePublishFailure recognizes ProblemDetails publishReadiness extension", () => {
    const err = axiosError({
      type: "https://abhyanvaya.dev/problems/publish-not-ready",
      title: "Timetable not ready to publish",
      detail: "Timetable is not ready to publish (1 blocking finding(s)).",
      publishReadiness: sampleReadiness(),
    });
    const parsed = parsePublishFailure(err);
    expect(parsed.kind).toBe("readiness");
    expect(parsed.readiness?.timetableId).toBe(50);
  });

  it("parsePublishFailure preserves string lifecycle DomainException bodies", () => {
    const messages = [
      "Frozen timetables cannot be republished until unlocked.",
      "Timetable must be locked or linked to an approved schedule version to publish.",
      "Another published timetable already exists for this academic year and department scope.",
    ];
    for (const message of messages) {
      const parsed = parsePublishFailure(axiosError(message));
      expect(parsed.kind).toBe("message");
      expect(parsed.message).toBe(message);
      expect(parsed.readiness).toBeNull();
    }
  });

  it("parsePublishFailure handles empty / malformed payloads safely without throwing", () => {
    expect(() => parsePublishFailure(axiosError(null))).not.toThrow();
    expect(() => parsePublishFailure(axiosError({}))).not.toThrow();
    expect(() => parsePublishFailure(axiosError({ foo: 1 }))).not.toThrow();
    expect(() => parsePublishFailure("not-an-error")).not.toThrow();

    expect(parsePublishFailure(axiosError(null)).kind).toBe("unknown");
    expect(parsePublishFailure(axiosError({})).kind).not.toBe("readiness");
    expect(normalizePublishReadiness(undefined)).toBeNull();
    expect(normalizePublishReadinessFinding(null)).toBeNull();
  });
});
