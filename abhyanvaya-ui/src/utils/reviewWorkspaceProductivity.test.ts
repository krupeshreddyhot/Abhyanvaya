import { describe, expect, it, beforeEach, vi } from "vitest";
import { RecognitionStatus, type AttendanceRecognitionReviewDto } from "../services/attendanceRecognitionService";
import { getHeatMapTone, HEAT_MAP_COLORS } from "./confidenceHeatMap";
import { buildReviewAnalytics, buildSessionProductivity } from "./reviewAnalytics";
import {
  loadReviewWorkspacePrefs,
  saveReviewWorkspacePrefs,
  setLastImageSequence,
  getLastImageSequence,
} from "./reviewWorkspacePrefs";
import {
  applySmartQueue,
  categorizeRecognition,
  countBySmartCategory,
  estimateReviewMinutesRemaining,
} from "./smartReviewQueue";

const base = (overrides: Partial<AttendanceRecognitionReviewDto>): AttendanceRecognitionReviewDto => ({
  recognitionId: "r1",
  attendanceSessionId: "s1",
  faceNumber: 1,
  imageSequence: 1,
  studentId: 10,
  studentNumber: "STU-10",
  studentName: "Ada Lovelace",
  confidence: 96,
  boundingBoxX: 0,
  boundingBoxY: 0,
  boundingBoxWidth: 10,
  boundingBoxHeight: 10,
  faceThumbnailUrl: null,
  studentPhotoUrl: null,
  status: RecognitionStatus.Recognized,
  isMatched: true,
  suggestedStudentId: 10,
  suggestedStudentName: "Ada Lovelace",
  suggestedStudentNumber: "STU-10",
  manualOverrideStudentId: null,
  manualOverrideStudentName: null,
  manualOverrideStudentNumber: null,
  verifiedByTeacher: false,
  teacherOverride: false,
  reviewNotes: null,
  ...overrides,
});

describe("reviewWorkspacePrefs (AI22.7A Phase 5.1)", () => {
  beforeEach(() => {
    const store = new Map<string, string>();
    vi.stubGlobal("localStorage", {
      getItem: (key: string) => store.get(key) ?? null,
      setItem: (key: string, value: string) => {
        store.set(key, value);
      },
      removeItem: (key: string) => {
        store.delete(key);
      },
      clear: () => store.clear(),
    });
  });

  it("persists fullscreen and heat map preferences", () => {
    saveReviewWorkspacePrefs({ fullscreen: true, heatMapEnabled: true, heatMapOpacity: 0.5 });
    const prefs = loadReviewWorkspacePrefs();
    expect(prefs.fullscreen).toBe(true);
    expect(prefs.heatMapEnabled).toBe(true);
    expect(prefs.heatMapOpacity).toBe(0.5);
  });

  it("remembers last selected image sequence per session", () => {
    setLastImageSequence("sess-a", 3);
    expect(getLastImageSequence("sess-a")).toBe(3);
    expect(getLastImageSequence("sess-b")).toBeNull();
  });
});

describe("smartReviewQueue (AI22.7A Phase 5.4)", () => {
  const rows = [
    base({ recognitionId: "needs", confidence: 88, verifiedByTeacher: false }),
    base({
      recognitionId: "unknown",
      status: RecognitionStatus.Unknown,
      isMatched: false,
      confidence: null,
    }),
    base({
      recognitionId: "dup",
      status: RecognitionStatus.Duplicate,
      confidence: 70,
    }),
    base({
      recognitionId: "low",
      confidence: 55,
      status: RecognitionStatus.LowConfidence,
    }),
    base({
      recognitionId: "rej",
      status: RecognitionStatus.Rejected,
      verifiedByTeacher: true,
    }),
    base({
      recognitionId: "ok",
      confidence: 97,
      verifiedByTeacher: true,
      status: RecognitionStatus.Recognized,
    }),
  ];

  it("categorizes recognitions for the smart queue", () => {
    expect(categorizeRecognition(rows[0])).toBe("needsReview");
    expect(categorizeRecognition(rows[1])).toBe("unknown");
    expect(categorizeRecognition(rows[2])).toBe("duplicate");
    expect(categorizeRecognition(rows[3])).toBe("lowConfidence");
    expect(categorizeRecognition(rows[4])).toBe("rejected");
    expect(categorizeRecognition(rows[5])).toBe("approved");
  });

  it("filters only pending and collapses approved", () => {
    const pending = applySmartQueue(rows, {
      category: "all",
      onlyPending: true,
      collapseApproved: true,
    });
    expect(pending.every((row) => row.recognitionId !== "ok")).toBe(true);
    expect(pending.every((row) => row.recognitionId !== "rej")).toBe(true);
  });

  it("counts categories and estimates remaining time", () => {
    const counts = countBySmartCategory(rows);
    expect(counts.approved).toBe(1);
    expect(counts.unknown).toBe(1);
    expect(estimateReviewMinutesRemaining(5, 12_000)).toBe(1);
  });
});

describe("confidenceHeatMap (AI22.7A Phase 5.5)", () => {
  it("maps confidence bands to heat tones and colors", () => {
    expect(getHeatMapTone(97)).toBe("high");
    expect(getHeatMapTone(80)).toBe("medium");
    expect(getHeatMapTone(55)).toBe("review");
    expect(getHeatMapTone(null)).toBe("unknown");
    expect(HEAT_MAP_COLORS.unknown).toBe("#d32f2f");
  });
});

describe("reviewAnalytics (AI22.7A Phase 5.6 / 5.8)", () => {
  it("builds analytics snapshot from existing DTOs", () => {
    const snapshot = buildReviewAnalytics({
      imageCount: 2,
      recognitions: [
        base({ recognitionId: "a", confidence: 90, verifiedByTeacher: true }),
        base({ recognitionId: "b", confidence: 40, status: RecognitionStatus.Unknown, isMatched: false }),
      ],
      statistics: null,
      elapsedMs: 120_000,
      averageDecisionMs: 10_000,
      pendingCount: 1,
    });
    expect(snapshot.images).toBe(2);
    expect(snapshot.faces).toBe(2);
    expect(snapshot.pending).toBe(1);
    expect(snapshot.confidenceBuckets.some((b) => b.count > 0)).toBe(true);
  });

  it("builds session productivity metrics without PII", () => {
    const now = Date.now();
    const metrics = buildSessionProductivity({
      elapsedMs: 60_000,
      recognitions: [
        base({ recognitionId: "a", verifiedByTeacher: true, studentId: 1 }),
        base({ recognitionId: "b", verifiedByTeacher: true, studentId: 2, status: RecognitionStatus.Rejected }),
        base({ recognitionId: "c", verifiedByTeacher: false }),
      ],
      decisionTimesMs: [now - 20_000, now - 10_000, now],
      pendingCount: 1,
    });
    expect(metrics.facesReviewed).toBe(2);
    expect(metrics.studentsReviewed).toBe(2);
    expect(metrics.approvalPercent).toBe(50);
    expect(metrics.sessionScore).toBeGreaterThan(0);
    expect(JSON.stringify(metrics)).not.toMatch(/Ada|Lovelace|STU-/);
  });
});
