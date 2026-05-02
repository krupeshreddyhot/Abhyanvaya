import api from "../api/axios";

export type IdName = { id: number; name: string };

export type CourseRow = IdName & { code: string };

export type GroupRow = { id: number; code: string; name: string; courseId: number; courseName: string };

export type SemesterRow = {
  id: number;
  number: number;
  name: string;
  courseId: number;
  courseName: string;
  groupId: number | null;
  groupName: string | null;
};

export type ElectiveGroupRow = {
  id: number;
  name: string;
  courseId: number;
  courseName: string;
  semesterId: number;
  semesterName: string;
  groupId: number;
  groupName: string;
};

export type SubjectCatalogRow = {
  id: number;
  tenantSubjectId: number;
  code: string | null;
  name: string;
  courseId: number;
  courseName: string;
  groupId: number;
  groupName: string;
  semesterId: number;
  semesterName: string;
  isElective: boolean;
  electiveGroupId: number | null;
  electiveGroupName: string | null;
  languageSubjectSlot: number;
  teachingLanguageId: number | null;
  teachingLanguageName: string | null;
  hpw: number | null;
  credits: number | null;
  examHours: number | null;
  marks: number | null;
};

export type TenantSubjectRow = {
  id: number;
  code: string | null;
  name: string;
};

export const listCourses = () => api.get<CourseRow[]>("/course");
export const createCourse = (payload: { code: string; name: string }) => api.post<CourseRow>("/course", payload);
export const updateCourse = (payload: { id: number; code: string; name: string }) => api.put<CourseRow>("/course", payload);

export const listGroups = () => api.get<GroupRow[]>("/group");
export const createGroup = (payload: { code: string; name: string; courseId: number }) => api.post<GroupRow>("/group", payload);
export const updateGroup = (payload: { id: number; code: string; name: string; courseId: number }) =>
  api.put<GroupRow>("/group", payload);

/** Uses tenant-scoped master endpoint so production JWT/role mapping matches courses/groups (AdminOnly /semester was brittle). */
export const listSemesters = () => api.get<SemesterRow[]>("/master/semesters/full");
export const createSemester = (payload: {
  number: number;
  name: string;
  courseId: number;
  groupId: number | null;
}) => api.post("/semester", payload);
export const updateSemester = (payload: {
  id: number;
  number: number;
  name: string;
  courseId: number;
  groupId: number | null;
}) => api.put("/semester", payload);

export const listSubjectCatalog = () => api.get<SubjectCatalogRow[]>("/subject/catalog");
export const createSubject = (payload: {
  tenantSubjectId: number;
  courseId: number;
  groupId: number;
  semesterId: number;
  isElective: boolean;
  electiveGroupId: number | null;
  languageSubjectSlot: number;
  teachingLanguageId: number | null;
  hpw: number | null;
  credits: number | null;
  examHours: number | null;
  marks: number | null;
}) => api.post("/subject", payload);
export const updateSubject = (payload: {
  id: number;
  tenantSubjectId: number;
  courseId: number;
  groupId: number;
  semesterId: number;
  isElective: boolean;
  electiveGroupId: number | null;
  languageSubjectSlot: number;
  teachingLanguageId: number | null;
  hpw: number | null;
  credits: number | null;
  examHours: number | null;
  marks: number | null;
}) => api.put("/subject", payload);
export const listTenantSubjects = (q?: string) =>
  api.get<TenantSubjectRow[]>("/subject/tenant-lookup", { params: q ? { q } : undefined });
export const createTenantSubject = (payload: { name: string; code: string | null }) =>
  api.post<TenantSubjectRow>("/subject/tenant-lookup", payload);

export const listLanguages = () => api.get<IdName[]>("/language");
export const createLanguage = (payload: { name: string }) => api.post<IdName>("/language", payload);
export const updateLanguage = (payload: { id: number; name: string }) => api.put<IdName>("/language", payload);

export const listGendersAdmin = () => api.get<IdName[]>("/gender");
export const createGender = (payload: { name: string }) => api.post<IdName>("/gender", payload);
export const updateGender = (payload: { id: number; name: string }) => api.put<IdName>("/gender", payload);

export const listMediumsAdmin = () => api.get<IdName[]>("/medium");
export const createMedium = (payload: { name: string }) => api.post<IdName>("/medium", payload);
export const updateMedium = (payload: { id: number; name: string }) => api.put<IdName>("/medium", payload);

export const listElectiveGroups = () => api.get<ElectiveGroupRow[]>("/elective-group");
export const createElectiveGroup = (payload: {
  name: string;
  courseId: number;
  semesterId: number;
  groupId: number;
}) => api.post("/elective-group", payload);
export const updateElectiveGroup = (payload: {
  id: number;
  name: string;
  courseId: number;
  semesterId: number;
  groupId: number;
}) => api.put("/elective-group", payload);
