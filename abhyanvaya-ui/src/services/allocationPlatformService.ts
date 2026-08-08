import api from "../api/axios";

export type AllocationScope = {
  academicYearId: number;
  courseId: number;
  groupId: number;
  semesterId: number;
};

export type SectionAllocationContext = {
  contextId: string;
  contextVersion: string;
  schemaVersion: string;
  generatedAt: string;
  checksum: string;
  hierarchy: {
    academicYearId: number;
    academicYearName?: string | null;
    programId?: number | null;
    programName?: string | null;
    courseId: number;
    courseName?: string | null;
    groupId: number;
    groupName?: string | null;
    semesterId: number;
    semesterName?: string | null;
  };
  sections: { sectionId: number; sectionCode: string; sectionName: string; sectionType: string; lifecycle: string; health: string; readiness: string }[];
  capacities: { sectionId: number; maximumCapacity: number; currentStrength: number; availableCapacity: number; occupancyPercent: number; capacityStatus: string }[];
  students: { studentId: number; studentNumber?: string | null; studentName?: string | null; currentSectionId?: number | null; currentSectionCode?: string | null }[];
  facultyAssignments: { facultyId: number; facultyName?: string | null; sectionId: number; role: string }[];
  subjectAssignments: { subjectId: number; subjectCode?: string | null; subjectName?: string | null }[];
  roomAvailability: { roomCode?: string | null; timetableMappingCount: number; status: string }[];
  policies: string[];
  recommendations: string[];
  overallHealth: string;
  overallReadiness: string;
  timetableStatus: string;
};

export type AllocationReadinessReport = {
  overallStatus: string;
  checks: { area: string; status: string; message: string }[];
};

export type AllocationHealthReport = {
  overallStatus: string;
  dimensions: { area: string; status: string; message: string }[];
};

export type AllocationValidationReport = {
  isValid: boolean;
  errors: string[];
  warnings: string[];
  checks: string[];
};

export type AllocationSnapshotDto = {
  snapshotId: string;
  contextVersion: string;
  schemaVersion: string;
  checksum: string;
  generatedDate: string;
};

const params = (scope: AllocationScope) => ({
  academicYearId: scope.academicYearId,
  courseId: scope.courseId,
  groupId: scope.groupId,
  semesterId: scope.semesterId,
});

export const getAllocationContext = (scope: AllocationScope, refresh = false) =>
  api.get<SectionAllocationContext>("/allocation/context", { params: { ...params(scope), refresh } });

export const getAllocationReadiness = (scope: AllocationScope) =>
  api.get<AllocationReadinessReport>("/allocation/readiness", { params: params(scope) });

export const getAllocationHealth = (scope: AllocationScope) =>
  api.get<AllocationHealthReport>("/allocation/health", { params: params(scope) });

export const getAllocationValidation = (scope: AllocationScope) =>
  api.get<AllocationValidationReport>("/allocation/validation", { params: params(scope) });

export const createAllocationSnapshot = (scope: AllocationScope) =>
  api.get<AllocationSnapshotDto>("/allocation/snapshot", { params: { ...params(scope), create: true } });

export const listAllocationSnapshots = (scope: AllocationScope) =>
  api.get<AllocationSnapshotDto[]>("/allocation/snapshot", { params: params(scope) });

export const getAllocationArchitectureReport = () =>
  api.get<{ passed: boolean; checks: string[]; violations: string[] }>("/allocation/architecture-report");
