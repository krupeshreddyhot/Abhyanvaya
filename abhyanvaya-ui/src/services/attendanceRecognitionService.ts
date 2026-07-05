import api from "../api/axios";

export const RecognitionStatus = {
  Unknown: 0,
  Recognized: 1,
  LowConfidence: 2,
  Duplicate: 3,
  Ignored: 4,
  Rejected: 5,
  ManuallyAssigned: 6,
} as const;

export type RecognitionStatusValue = (typeof RecognitionStatus)[keyof typeof RecognitionStatus];

export const RecognitionReviewAction = {
  Approve: 1,
  Reject: 2,
  Ignore: 3,
  AssignStudent: 4,
  Reset: 5,
} as const;

export type RecognitionReviewActionValue =
  (typeof RecognitionReviewAction)[keyof typeof RecognitionReviewAction];

export type AttendanceRecognitionReviewDto = {
  recognitionId: string;
  attendanceSessionId: string;
  faceNumber: number;
  studentId: number | null;
  studentNumber: string | null;
  studentName: string | null;
  confidence: number | null;
  boundingBoxX: number;
  boundingBoxY: number;
  boundingBoxWidth: number;
  boundingBoxHeight: number;
  faceThumbnailUrl: string | null;
  studentPhotoUrl: string | null;
  status: RecognitionStatusValue;
  isMatched: boolean;
  suggestedStudentId: number | null;
  suggestedStudentName: string | null;
  suggestedStudentNumber: string | null;
  manualOverrideStudentId: number | null;
  manualOverrideStudentName: string | null;
  manualOverrideStudentNumber: string | null;
  verifiedByTeacher: boolean;
  teacherOverride: boolean;
  reviewNotes: string | null;
};

export type RecognitionStatisticsDto = {
  detectedFaces: number;
  matched: number;
  unmatched: number;
  lowConfidence: number;
  manualOverrides: number;
  rejected: number;
  approved: number;
  pendingReview: number;
  averageConfidence: number | null;
};

export type RecognitionSummaryDto = {
  attendanceSessionId: string;
  statistics: RecognitionStatisticsDto;
  canFinalize: boolean;
  finalizeBlockers: string[];
};

/** Mutation responses remain compact for review commands. */
export type AttendanceRecognitionDto = {
  id: string;
  attendanceSessionId: string;
  studentId: number | null;
  studentName: string | null;
  studentNumber: string | null;
  thumbnailUrl: string | null;
  confidenceScore: number | null;
  embeddingDistance: number | null;
  recognitionStatus: RecognitionStatusValue;
  boundingBoxX: number;
  boundingBoxY: number;
  boundingBoxWidth: number;
  boundingBoxHeight: number;
  verifiedByTeacher: boolean;
  teacherOverride: boolean;
  reviewNotes: string | null;
};

export type AttendanceRecognitionReviewHistoryDto = {
  id: string;
  recognitionId: string;
  oldStatus: RecognitionStatusValue;
  newStatus: RecognitionStatusValue;
  oldStudentId: number | null;
  newStudentId: number | null;
  reviewAction: RecognitionReviewActionValue;
  reviewNotes: string | null;
  reviewedBy: number;
  reviewedByUsername: string | null;
  reviewedUtc: string;
};

export type AttendanceSessionReviewDto = {
  id: string;
  status: number;
  attendanceDate: string;
  annotatedImageUrl: string | null;
  originalImageUrl: string | null;
  imageWidth: number | null;
  imageHeight: number | null;
};

export type AttendanceBuildSummaryDto = {
  attendanceSessionId: string;
  present: number;
  absent: number;
  ignored: number;
  rejected: number;
  unknown: number;
  manualCorrections: number;
  totalStudents: number;
  generatedUtc: string | null;
  durationMilliseconds: number | null;
  alreadyFinalized: boolean;
};

export type FinalizationStatusDto = {
  attendanceSessionId: string;
  canFinalize: boolean;
  blockingReasons: string[];
  pendingRecognitions: number;
  reviewedRecognitions: number;
  manualOverrides: number;
  rejectedRecognitions: number;
  unknownFaces: number;
  attendanceAlreadyGenerated: boolean;
  studentsPresent: number;
  studentsAbsent: number;
  totalStudents: number;
  readyToFinalize: boolean;
  attendanceDate: string;
  facultyName: string | null;
  subjectName: string | null;
};

export type AttendanceSessionReportDto = {
  attendanceSessionId: string;
  present: number;
  absent: number;
  recognitionAccuracy: number | null;
  manualCorrections: number;
  reviewTimeMilliseconds: number | null;
  finalizationTime: string | null;
};

export type AuditEntryDto = {
  id: number;
  entityName: string;
  action: number;
  oldValues: string | null;
  newValues: string | null;
  performedBy: number | null;
  performedByUsername: string | null;
  performedUtc: string;
};

export type AttendanceRecognitionReviewRequest = {
  recognitionId: string;
  action: RecognitionReviewActionValue;
  studentId?: number | null;
  reviewNotes?: string | null;
};

export function mergeReviewUpdate(
  row: AttendanceRecognitionReviewDto,
  updated: AttendanceRecognitionDto
): AttendanceRecognitionReviewDto {
  return {
    ...row,
    studentId: updated.studentId,
    studentName: updated.studentName,
    studentNumber: updated.studentNumber,
    confidence: updated.confidenceScore ?? row.confidence,
    faceThumbnailUrl: updated.thumbnailUrl ?? row.faceThumbnailUrl,
    status: updated.recognitionStatus,
    verifiedByTeacher: updated.verifiedByTeacher,
    teacherOverride: updated.teacherOverride,
    reviewNotes: updated.reviewNotes,
    isMatched:
      updated.studentId != null
        && (updated.recognitionStatus === RecognitionStatus.Recognized
          || updated.recognitionStatus === RecognitionStatus.LowConfidence
          || updated.recognitionStatus === RecognitionStatus.ManuallyAssigned),
    suggestedStudentId: updated.teacherOverride ? row.suggestedStudentId : updated.studentId,
    suggestedStudentName: updated.teacherOverride ? row.suggestedStudentName : updated.studentName,
    suggestedStudentNumber: updated.teacherOverride ? row.suggestedStudentNumber : updated.studentNumber,
    manualOverrideStudentId: updated.teacherOverride ? updated.studentId : null,
    manualOverrideStudentName: updated.teacherOverride ? updated.studentName : null,
    manualOverrideStudentNumber: updated.teacherOverride ? updated.studentNumber : null,
  };
}

export const getAttendanceSession = async (sessionId: string) =>
  api.get<AttendanceSessionReviewDto>(`/attendance-sessions/${sessionId}`);

export const getSessionRecognitions = async (sessionId: string) =>
  api.get<AttendanceRecognitionReviewDto[]>(`/attendance-sessions/${sessionId}/recognitions`);

export const getRecognitionSummary = async (sessionId: string) =>
  api.get<RecognitionSummaryDto>(`/attendance-sessions/${sessionId}/recognition-summary`);

export const getFinalizationStatus = async (sessionId: string) =>
  api.get<FinalizationStatusDto>(`/attendance-sessions/${sessionId}/finalization-status`);

export const getSessionReport = async (sessionId: string) =>
  api.get<AttendanceSessionReportDto>(`/attendance-sessions/${sessionId}/report`);

export const getSessionAuditEntries = async (sessionId: string) =>
  api.get<AuditEntryDto[]>(`/attendance-sessions/${sessionId}/audit-entries`);

export const getSessionReviewHistory = async (sessionId: string) =>
  api.get<AttendanceRecognitionReviewHistoryDto[]>(
    `/attendance-sessions/${sessionId}/recognition-review-history`
  );

export const reviewRecognition = async (payload: AttendanceRecognitionReviewRequest) =>
  api.post<AttendanceRecognitionDto>("/attendance-recognition/review", payload);

export const reviewRecognitionBatch = async (payload: {
  attendanceSessionId: string;
  reviews: AttendanceRecognitionReviewRequest[];
}) => api.post<AttendanceRecognitionDto[]>("/attendance-recognition/review-batch", payload);

export const resetRecognition = async (recognitionId: string) =>
  api.delete<AttendanceRecognitionDto>(`/attendance-recognition/${recognitionId}/reset`);

export const finalizeAttendanceSession = async (sessionId: string) =>
  api.post<AttendanceBuildSummaryDto>(`/attendance-sessions/${sessionId}/finalize`);
