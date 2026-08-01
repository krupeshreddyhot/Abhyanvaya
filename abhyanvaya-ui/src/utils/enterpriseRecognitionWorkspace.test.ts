import { describe, expect, it } from "vitest";
import { RecognitionStatus, type AttendanceRecognitionReviewDto } from "../services/attendanceRecognitionService";
import { getEnterpriseConfidence } from "./enterpriseConfidence";
import {
  filterRecognitions,
  getRelatedRecognitionIds,
} from "./recognitionReviewFilters";

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

describe("enterpriseConfidence (AI22.7A Phase 4.3)", () => {
  it("maps bands to enterprise labels and bbox colors", () => {
    expect(getEnterpriseConfidence(99).label).toBe("Excellent");
    expect(getEnterpriseConfidence(99).stars).toBe("★★★★★");
    expect(getEnterpriseConfidence(90).filterId).toBe("high");
    expect(getEnterpriseConfidence(80).filterId).toBe("medium");
    expect(getEnterpriseConfidence(60).filterId).toBe("low");
    expect(getEnterpriseConfidence(null).filterId).toBe("unknown");
    expect(getEnterpriseConfidence(95).bboxColor).toBe("#2e7d32");
    expect(getEnterpriseConfidence(75).bboxColor).toBe("#f9a825");
  });
});

describe("recognitionReviewFilters (AI22.7A Phase 4.2–4.4)", () => {
  const rows = [
    base({ recognitionId: "a", studentId: 1, confidence: 98, imageSequence: 1 }),
    base({
      recognitionId: "b",
      studentId: 1,
      confidence: 72,
      imageSequence: 2,
      faceNumber: 2,
      studentName: "Ada Lovelace",
    }),
    base({
      recognitionId: "c",
      studentId: 2,
      confidence: 40,
      status: RecognitionStatus.Rejected,
      verifiedByTeacher: true,
      isMatched: false,
      studentName: "Grace Hopper",
      studentNumber: "GH-2",
      suggestedStudentId: 2,
      suggestedStudentName: "Grace Hopper",
      suggestedStudentNumber: "GH-2",
    }),
  ];

  it("finds related faces across images for the same student", () => {
    const related = getRelatedRecognitionIds(rows, rows[0]);
    expect(related.has("a")).toBe(true);
    expect(related.has("b")).toBe(true);
    expect(related.has("c")).toBe(false);
  });

  it("searches by name and student number", () => {
    const byName = filterRecognitions(rows, new Set(), "grace");
    expect(byName.map((r) => r.recognitionId)).toEqual(["c"]);
    const byNumber = filterRecognitions(rows, new Set(), "stu-10");
    expect(byNumber.map((r) => r.recognitionId).sort()).toEqual(["a", "b"]);
  });

  it("hides high confidence when requested", () => {
    const filtered = filterRecognitions(rows, new Set(), "", { hideHighConfidence: true });
    expect(filtered.map((r) => r.recognitionId).sort()).toEqual(["b", "c"]);
  });

  it("filters by confidence band", () => {
    const excellent = filterRecognitions(rows, new Set(["confidenceExcellent"]), "");
    expect(excellent.map((r) => r.recognitionId)).toEqual(["a"]);
  });
});
