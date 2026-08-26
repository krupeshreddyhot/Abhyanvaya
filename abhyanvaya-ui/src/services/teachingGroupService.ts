import api from "../api/axios";

/** Mirrors backend TeachingGroupType (byte). */
export const TeachingGroupType = {
  SectionDerived: 1,
  CombinedSections: 2,
  StudentSubset: 3,
  Elective: 4,
  Laboratory: 5,
  CapacitySplit: 6,
  Custom: 7,
} as const;
export type TeachingGroupType = (typeof TeachingGroupType)[keyof typeof TeachingGroupType];

/** Mirrors backend TeachingGroupMembershipSource (byte). */
export const TeachingGroupMembershipSource = {
  Section: 1,
  CombinedSections: 2,
  StudentSubject: 3,
  ExplicitStudents: 4,
  Hybrid: 5,
} as const;
export type TeachingGroupMembershipSource =
  (typeof TeachingGroupMembershipSource)[keyof typeof TeachingGroupMembershipSource];

/** Mirrors backend TeachingGroupStatus (byte). */
export const TeachingGroupStatus = {
  Draft: 1,
  Active: 2,
  Locked: 3,
  Archived: 4,
} as const;
export type TeachingGroupStatus = (typeof TeachingGroupStatus)[keyof typeof TeachingGroupStatus];

/** Mirrors backend TeachingGroupActivityKind (byte). */
export const TeachingGroupActivityKind = {
  Lecture: 1,
  Laboratory: 2,
  Tutorial: 3,
  Seminar: 4,
  Other: 5,
} as const;
export type TeachingGroupActivityKind =
  (typeof TeachingGroupActivityKind)[keyof typeof TeachingGroupActivityKind];

export type TeachingGroupSummaryDto = {
  id: number;
  code: string | null;
  name: string;
  type: TeachingGroupType;
  status: TeachingGroupStatus;
  membershipSource: TeachingGroupMembershipSource;
  activityKind: TeachingGroupActivityKind;
  subjectAllocationId: number;
  academicYearId: number;
  courseId: number;
  groupId: number;
  semesterId: number;
  subjectId: number;
  expectedStudentCount: number | null;
  maxTeachingCapacity: number | null;
  /** Derived from membership — never edit in UI. */
  resolvedStudentCount: number;
  linkedSectionCount: number;
  timetableEntryCount: number;
  exclusionGroupKey: string | null;
  effectiveFrom: string;
  effectiveTo: string | null;
};

export type TeachingGroupSectionDto = {
  id: number;
  teachingGroupId: number;
  sectionId: number;
  isPrimary: boolean;
  sectionCode?: string | null;
  sectionName?: string | null;
};

export type TeachingGroupDetailDto = TeachingGroupSummaryDto & {
  displayOrder: number;
  notes: string | null;
  membershipCount: number;
  sections: TeachingGroupSectionDto[];
};

/** Mirrors backend TeachingGroupMembershipInclusion (byte). */
export const TeachingGroupMembershipInclusion = {
  Include: 1,
  Exclude: 2,
} as const;
export type TeachingGroupMembershipInclusion =
  (typeof TeachingGroupMembershipInclusion)[keyof typeof TeachingGroupMembershipInclusion];

/** Mirrors backend TeachingGroupMemberProvenance (byte). */
export const TeachingGroupMemberProvenance = {
  Derived: 1,
  ExplicitInclude: 2,
} as const;
export type TeachingGroupMemberProvenance =
  (typeof TeachingGroupMemberProvenance)[keyof typeof TeachingGroupMemberProvenance];

export type TeachingGroupMembershipDto = {
  id: number;
  teachingGroupId: number;
  studentId: number;
  inclusion: TeachingGroupMembershipInclusion | number;
  effectiveFrom: string;
  effectiveTo: string | null;
  isCurrent: boolean;
};

/** Server-resolved roster row — do not recompute Base ∪ Includes − Excludes in the client. */
export type ResolvedTeachingGroupMemberDto = {
  studentId: number;
  provenance: TeachingGroupMemberProvenance | number;
};

export type AddTeachingGroupMembersRequest = {
  studentIds: number[];
  effectiveFrom?: string | null;
};

export type ReplaceTeachingGroupMembershipsRequest = {
  includeStudentIds: number[];
  /** Hybrid only. Must be empty for ExplicitStudents (server enforces). */
  excludeStudentIds?: number[];
};

export type TeachingGroupMembershipMutationResultDto = {
  teachingGroupId: number;
  resolvedStudentCount: number;
  memberships: TeachingGroupMembershipDto[];
  resolvedMembers: ResolvedTeachingGroupMemberDto[];
};

export type CreateTeachingGroupRequest = {
  subjectAllocationId: number;
  name: string;
  code?: string | null;
  type: TeachingGroupType;
  membershipSource: TeachingGroupMembershipSource;
  activityKind?: TeachingGroupActivityKind;
  expectedStudentCount?: number | null;
  maxTeachingCapacity?: number | null;
  exclusionGroupKey?: string | null;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  notes?: string | null;
  displayOrder?: number;
};

export type UpdateTeachingGroupRequest = {
  name: string;
  code?: string | null;
  activityKind: TeachingGroupActivityKind;
  expectedStudentCount?: number | null;
  maxTeachingCapacity?: number | null;
  exclusionGroupKey?: string | null;
  effectiveFrom: string;
  effectiveTo?: string | null;
  notes?: string | null;
  displayOrder?: number;
};

export type ReplaceTeachingGroupSectionsRequest = {
  sectionIds: number[];
};

export type AddTeachingGroupSectionRequest = {
  isPrimary?: boolean;
};

const base = "/scheduling/teaching-groups";

/** List Teaching Groups for a SubjectAllocation. Never auto-creates. */
export const listTeachingGroups = (subjectAllocationId: number) =>
  api.get<TeachingGroupSummaryDto[]>(base, { params: { subjectAllocationId } });

export const getTeachingGroup = (id: number) => api.get<TeachingGroupDetailDto>(`${base}/${id}`);

export const createTeachingGroup = (payload: CreateTeachingGroupRequest) =>
  api.post<TeachingGroupDetailDto>(base, payload);

export const updateTeachingGroup = (id: number, payload: UpdateTeachingGroupRequest) =>
  api.put<TeachingGroupDetailDto>(`${base}/${id}`, payload);

export const archiveTeachingGroup = (id: number) =>
  api.post<TeachingGroupDetailDto>(`${base}/${id}/archive`);

export const getTeachingGroupMemberships = (id: number) =>
  api.get<TeachingGroupMembershipDto[]>(`${base}/${id}/memberships`);

/** Resolved roster from the server membership resolver (transport only — no client recalculation). */
export const getResolvedTeachingGroupMembers = (id: number) =>
  api.get<ResolvedTeachingGroupMemberDto[]>(`${base}/${id}/resolved-members`);

export const addTeachingGroupMembers = (id: number, payload: AddTeachingGroupMembersRequest) =>
  api.post<TeachingGroupMembershipMutationResultDto>(`${base}/${id}/memberships`, payload);

export const replaceTeachingGroupMemberships = (
  id: number,
  payload: ReplaceTeachingGroupMembershipsRequest,
) => api.put<TeachingGroupMembershipMutationResultDto>(`${base}/${id}/memberships`, payload);

export const removeTeachingGroupMember = (id: number, studentId: number) =>
  api.delete<TeachingGroupMembershipMutationResultDto>(`${base}/${id}/memberships/${studentId}`);

export const getTeachingGroupSections = (id: number) =>
  api.get<TeachingGroupSectionDto[]>(`${base}/${id}/sections`);

export const replaceTeachingGroupSections = (id: number, payload: ReplaceTeachingGroupSectionsRequest) =>
  api.put<TeachingGroupSectionDto[]>(`${base}/${id}/sections`, payload);

export const addTeachingGroupSection = (
  id: number,
  sectionId: number,
  payload?: AddTeachingGroupSectionRequest,
) => api.post<TeachingGroupSectionDto>(`${base}/${id}/sections/${sectionId}`, payload ?? {});

export const removeTeachingGroupSection = (id: number, sectionId: number) =>
  api.delete(`${base}/${id}/sections/${sectionId}`);
