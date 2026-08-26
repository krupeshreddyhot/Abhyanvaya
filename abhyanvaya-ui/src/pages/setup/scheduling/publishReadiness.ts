import axios from "axios";
import {
  getTimetablePublishReadiness,
  type PublishReadinessFindingDto,
  type TimetablePublishReadinessResultDto,
  type TimetableStatus,
} from "../../../services/schedulingService";
import { errMsg } from "./schedulingFormUtils";

/** Re-export API client for PublishingPage / designer / Prompt 8.3 consumers. */
export { getTimetablePublishReadiness };

/** Known publish-readiness finding codes (presentation/navigation only — not client blocking rules). */
export const PUBLISH_READINESS_CODES = {
  RoomCapacity: "ROOM_CAPACITY",
  TeachingGroupCapacityExceeded: "TEACHING_GROUP_CAPACITY_EXCEEDED",
  LifecycleFrozen: "LIFECYCLE_FROZEN",
  LifecycleNotEligible: "LIFECYCLE_NOT_ELIGIBLE",
  LifecycleArchived: "LIFECYCLE_ARCHIVED",
  LifecyclePublishedScopeConflict: "LIFECYCLE_PUBLISHED_SCOPE_CONFLICT",
} as const;

export type PublishFailureKind = "readiness" | "message" | "unknown";

export type PublishFailureResult =
  | {
      kind: "readiness";
      readiness: TimetablePublishReadinessResultDto;
      /** Short summary for existing Alert surfaces until Prompt 8.3 dialog. */
      message: string;
    }
  | {
      kind: "message";
      message: string;
      readiness: null;
    }
  | {
      kind: "unknown";
      message: string;
      readiness: null;
    };

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === "object" && value !== null && !Array.isArray(value);

const asOptionalNumber = (value: unknown): number | null | undefined => {
  if (value === undefined) return undefined;
  if (value === null) return null;
  return typeof value === "number" && Number.isFinite(value) ? value : undefined;
};

const asString = (value: unknown, fallback = ""): string =>
  typeof value === "string" ? value : fallback;

const asBoolean = (value: unknown, fallback = false): boolean =>
  typeof value === "boolean" ? value : fallback;

const asNumber = (value: unknown, fallback = 0): number =>
  typeof value === "number" && Number.isFinite(value) ? value : fallback;

/**
 * Normalize a finding from the server payload.
 * Trusts server `isBlocking` — never derives blocking from severity or code.
 */
export const normalizePublishReadinessFinding = (
  raw: unknown,
): PublishReadinessFindingDto | null => {
  if (!isRecord(raw)) return null;
  const code = asString(raw.code).trim();
  if (!code) return null;

  return {
    code,
    severity: asString(raw.severity, "Error"),
    isBlocking: asBoolean(raw.isBlocking, false),
    title: asString(raw.title, code),
    why: asString(raw.why),
    recommendedAction: asString(raw.recommendedAction),
    timetableEntryId: asOptionalNumber(raw.timetableEntryId),
    dayOfWeek: asOptionalNumber(raw.dayOfWeek),
    timeSlotId: asOptionalNumber(raw.timeSlotId),
    roomId: asOptionalNumber(raw.roomId),
    teachingGroupId: asOptionalNumber(raw.teachingGroupId),
    teachingGroupCode: typeof raw.teachingGroupCode === "string" ? raw.teachingGroupCode : null,
    teachingGroupName: typeof raw.teachingGroupName === "string" ? raw.teachingGroupName : null,
    teachingGroupStatus: typeof raw.teachingGroupStatus === "string" ? raw.teachingGroupStatus : null,
    placementSize: asOptionalNumber(raw.placementSize),
    placementSizeSource: typeof raw.placementSizeSource === "string" ? raw.placementSizeSource : null,
    roomCapacity: asOptionalNumber(raw.roomCapacity),
    capacityMarginPercent:
      typeof raw.capacityMarginPercent === "number" ? raw.capacityMarginPercent : null,
    effectiveRoomCapacity:
      typeof raw.effectiveRoomCapacity === "number" ? raw.effectiveRoomCapacity : null,
    resolvedStudentCount: asOptionalNumber(raw.resolvedStudentCount),
    maxTeachingCapacity: asOptionalNumber(raw.maxTeachingCapacity),
  };
};

/**
 * Normalize a readiness DTO from GET publish-readiness or publish 400 body.
 * Returns null when the payload is not a readiness result.
 */
export const normalizePublishReadiness = (
  raw: unknown,
): TimetablePublishReadinessResultDto | null => {
  if (!isRecord(raw)) return null;

  // ProblemDetails extension shape from GlobalExceptionHandler
  if (isRecord(raw.publishReadiness)) {
    return normalizePublishReadiness(raw.publishReadiness);
  }

  const hasIsReady = typeof raw.isReady === "boolean";
  const hasFindings = Array.isArray(raw.findings);
  const hasTimetableId = typeof raw.timetableId === "number";

  // Discriminate readiness from unrelated JSON objects.
  if (!hasIsReady && !(hasTimetableId && hasFindings)) {
    return null;
  }

  const findings = (hasFindings ? (raw.findings as unknown[]) : [])
    .map(normalizePublishReadinessFinding)
    .filter((f): f is PublishReadinessFindingDto => f !== null);

  const blockers = findings.filter((f) => f.isBlocking);
  const isReady = hasIsReady ? (raw.isReady as boolean) : blockers.length === 0;

  return {
    timetableId: asNumber(raw.timetableId),
    lifecycleState: asNumber(raw.lifecycleState, 0) as TimetableStatus,
    isFrozen: asBoolean(raw.isFrozen, false),
    isReady,
    blockingFindingCount: asNumber(raw.blockingFindingCount, blockers.length),
    warningFindingCount: asNumber(raw.warningFindingCount, 0),
    informationalFindingCount: asNumber(raw.informationalFindingCount, 0),
    evaluatedAtUtc: asString(raw.evaluatedAtUtc, new Date(0).toISOString()),
    findings,
  };
};

/** Server-authoritative blockers only (`isBlocking === true`). */
export const getPublishBlockers = (
  readiness: TimetablePublishReadinessResultDto | null | undefined,
): PublishReadinessFindingDto[] => {
  if (!readiness) return [];
  return readiness.findings.filter((f: PublishReadinessFindingDto) => f.isBlocking);
};

export const getPublishNonBlockingFindings = (
  readiness: TimetablePublishReadinessResultDto | null | undefined,
): PublishReadinessFindingDto[] => {
  if (!readiness) return [];
  return readiness.findings.filter((f: PublishReadinessFindingDto) => !f.isBlocking);
};

export const isPublishReadinessPayload = (raw: unknown): boolean =>
  normalizePublishReadiness(raw) !== null;

export const formatPublishReadinessSummary = (
  readiness: TimetablePublishReadinessResultDto,
): string => {
  const blockers = getPublishBlockers(readiness);
  if (blockers.length === 0) {
    return readiness.isReady
      ? "Timetable is ready to publish."
      : "Timetable is not ready to publish.";
  }

  const titles = blockers
    .slice(0, 3)
    .map((b) => b.title || b.code)
    .join("; ");
  const more = blockers.length > 3 ? ` (+${blockers.length - 3} more)` : "";
  return `Timetable is not ready to publish (${blockers.length} blocking finding(s)): ${titles}${more}`;
};

/**
 * Parse a publish (or readiness-related) failure from axios / unknown errors.
 * Never throws. Preserves string lifecycle DomainException bodies.
 */
export const parsePublishFailure = (error: unknown): PublishFailureResult => {
  try {
    if (axios.isAxiosError(error)) {
      const data = error.response?.data;

      if (typeof data === "string" && data.trim()) {
        return { kind: "message", message: data.trim(), readiness: null };
      }

      if (data !== undefined && data !== null && data !== "") {
        const readiness = normalizePublishReadiness(data);
        if (readiness && (!readiness.isReady || getPublishBlockers(readiness).length > 0)) {
          return {
            kind: "readiness",
            readiness,
            message: formatPublishReadinessSummary(readiness),
          };
        }
        // Non-readiness object (e.g. ProblemDetails without extension)
        if (isRecord(data)) {
          const detail =
            (typeof data.detail === "string" && data.detail.trim()) ||
            (typeof data.title === "string" && data.title.trim()) ||
            (typeof data.message === "string" && data.message.trim());
          if (detail) {
            return { kind: "message", message: detail, readiness: null };
          }
        }
      }

      if (!error.response) {
        return {
          kind: "unknown",
          message: "Network error. Check your connection and try again.",
          readiness: null,
        };
      }

      return {
        kind: "unknown",
        message: "Timetable could not be published.",
        readiness: null,
      };
    }

    if (error instanceof Error && error.message.trim()) {
      return { kind: "message", message: error.message.trim(), readiness: null };
    }

    const fallback = errMsg(error);
    return {
      kind: fallback === "Request failed." ? "unknown" : "message",
      message: fallback === "Request failed." ? "Timetable could not be published." : fallback,
      readiness: null,
    };
  } catch {
    return {
      kind: "unknown",
      message: "Timetable could not be published.",
      readiness: null,
    };
  }
};

/** @deprecated Prefer parsePublishFailure — alias for Prompt 8.2 naming. */
export const parsePublishReadinessError = parsePublishFailure;

export const isPublishReadinessError = (error: unknown): boolean =>
  parsePublishFailure(error).kind === "readiness";
