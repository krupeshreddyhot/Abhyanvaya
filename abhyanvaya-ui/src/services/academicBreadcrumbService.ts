import api from "../api/axios";

/**
 * AI29.1A.6 / AI29.1D Prompt 16 — Academic breadcrumb API client.
 * Prefer these endpoints over reconstructing hierarchy labels in page components.
 */

export type AcademicBreadcrumbItemDto = {
  nodeId: string;
  entityType: string;
  entityId: number;
  displayName: string;
  code: string;
};

export type AcademicBreadcrumbDto = {
  items: AcademicBreadcrumbItemDto[];
  displayPath?: string;
};

export type AcademicOperationalContextQuery = {
  programId?: number | null;
  courseId?: number | null;
  groupId?: number | null;
  semesterId?: number | null;
  sectionId?: number | null;
  sectionIds?: number[] | null;
  subjectId?: number | null;
};

const cleanParams = (q: AcademicOperationalContextQuery) => {
  const params: Record<string, number | number[]> = {};
  if (q.programId != null && q.programId > 0) params.programId = q.programId;
  if (q.courseId != null && q.courseId > 0) params.courseId = q.courseId;
  if (q.groupId != null && q.groupId > 0) params.groupId = q.groupId;
  if (q.semesterId != null && q.semesterId > 0) params.semesterId = q.semesterId;
  if (q.sectionId != null && q.sectionId > 0) params.sectionId = q.sectionId;
  if (q.subjectId != null && q.subjectId > 0) params.subjectId = q.subjectId;
  const sectionIds = (q.sectionIds ?? []).filter((id) => id > 0);
  if (sectionIds.length > 0) params.sectionIds = sectionIds;
  return params;
};

export const hasAcademicContextSelection = (q: AcademicOperationalContextQuery): boolean =>
  Object.keys(cleanParams(q)).length > 0;

/** Operational trail: Program? → Course → Group → Semester → Section? → Subject? */
export const getAcademicContextBreadcrumb = (query: AcademicOperationalContextQuery) =>
  api.get<AcademicBreadcrumbDto>("/v1/academic-structure/breadcrumb/context", {
    params: cleanParams(query),
    paramsSerializer: {
      indexes: null, // sectionIds=1&sectionIds=2
    },
  });

export const getAcademicNodeBreadcrumb = (nodeId: string) =>
  api.get<AcademicBreadcrumbDto>("/v1/academic-structure/breadcrumb", { params: { nodeId } });
