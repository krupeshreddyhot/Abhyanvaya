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
/** Tenant-scoped read via Master API — use on setup pages where the user may lack `Setup.Courses.Manage`. */
export const listMasterCourses = () => api.get<CourseRow[]>("/master/courses");
export const createCourse = (payload: { code: string; name: string }) => api.post<CourseRow>("/course", payload);
export const updateCourse = (payload: { id: number; code: string; name: string }) => api.put<CourseRow>("/course", payload);

export const listGroups = () => api.get<GroupRow[]>("/group");
/** Tenant-scoped read via Master API — includes `courseName`; use when the user may lack `Setup.Groups.Manage`. */
export const listMasterGroups = () => api.get<GroupRow[]>("/master/groups");
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

// --- Departments & Staff (tenant admin) ---

export type CollegeSummary = { id: number; name: string; code: string };

export type LookupItem = {
  id: number;
  name: string;
  code: string | null;
  sortOrder: number;
};

export type StaffSetupMetadata = {
  staffTypes: LookupItem[];
  personTitles: LookupItem[];
  designations: LookupItem[];
  qualifications: LookupItem[];
  employmentStatuses: LookupItem[];
  departmentRoles: LookupItem[];
  collegeRoles: LookupItem[];
  colleges: CollegeSummary[];
};

export const getStaffSetupMetadata = () => api.get<StaffSetupMetadata>("/staff/setup-metadata");

export type DepartmentRow = {
  id: number;
  collegeId: number;
  name: string;
  code: string | null;
  sortOrder: number;
};

export const listDepartments = (collegeId?: number) =>
  api.get<DepartmentRow[]>("/department", { params: collegeId != null && collegeId > 0 ? { collegeId } : undefined });

export const createDepartment = (payload: {
  collegeId: number;
  name: string;
  code: string | null;
  sortOrder: number;
}) => api.post<DepartmentRow>("/department", payload);

export const updateDepartment = (
  id: number,
  payload: {
    name: string;
    code: string | null;
    sortOrder: number;
  },
) => api.put(`/department/${id}`, payload);

export const deleteDepartment = (id: number) => api.delete(`/department/${id}`);

export type StaffDepartmentAssignment = {
  departmentId: number;
  departmentRoleLookupIds: number[];
};

export type CreateStaffPayload = {
  collegeId: number;
  staffCode: string | null;
  staffTypeId: number;
  personTitleId: number | null;
  designationId: number;
  qualificationId: number | null;
  genderId: number | null;
  employmentStatusId: number | null;
  firstName: string;
  lastName: string;
  phone: string | null;
  altPhone: string | null;
  email: string | null;
  website: string | null;
  dateOfJoining: string | null;
  contractEndDate: string | null;
  dateOfBirth: string | null;
  departments: StaffDepartmentAssignment[] | null;
  collegeRoleLookupIds: number[] | null;
  subjectIds: number[] | null;
};

export type StaffListItem = {
  id: number;
  collegeId: number;
  staffCode: string | null;
  firstName: string;
  lastName: string;
  staffTypeName: string;
  designationName: string;
  email: string | null;
  dateOfJoining: string | null;
};

export type StaffDetail = {
  id: number;
  collegeId: number;
  staffCode: string | null;
  staffTypeId: number;
  personTitleId: number | null;
  designationId: number;
  qualificationId: number | null;
  genderId: number | null;
  employmentStatusId: number | null;
  firstName: string;
  lastName: string;
  phone: string | null;
  altPhone: string | null;
  email: string | null;
  website: string | null;
  dateOfJoining: string | null;
  contractEndDate: string | null;
  dateOfBirth: string | null;
  departments: StaffDepartmentAssignment[];
  collegeRoleLookupIds: number[];
  subjectIds: number[];
};

export const listStaff = (params?: { collegeId?: number; search?: string; page?: number; pageSize?: number }) =>
  api.get<{ total: number; page: number; pageSize: number; items: StaffListItem[] }>("/staff", { params });

export const getStaff = (id: number) => api.get<StaffDetail>(`/staff/${id}`);

export const createStaff = (payload: CreateStaffPayload) => api.post<{ id: number }>("/staff", payload);

export const updateStaff = (id: number, payload: CreateStaffPayload) => api.put(`/staff/${id}`, payload);

export const deleteStaff = (id: number) => api.delete(`/staff/${id}`);

/** Staff / department-role lookup rows used by Staff setup (tenant-scoped). */
export type StaffHubLookupKind =
  | "staff-types"
  | "person-titles"
  | "designations"
  | "qualifications"
  | "employment-statuses"
  | "department-roles"
  | "college-roles";

export type StaffLookupAdminRow = {
  id: number;
  name: string;
  code: string | null;
  sortOrder: number;
  isActive: boolean;
  isExclusivePerDepartment: boolean;
  isExclusivePerCollege: boolean;
};

export type StaffLookupWritePayload = {
  name: string;
  code?: string | null;
  sortOrder: number;
  isActive: boolean;
  isExclusivePerDepartment: boolean;
  isExclusivePerCollege: boolean;
};

const staffHubLookupsBase = "/staff-hub-lookups";

export const listStaffHubLookups = (kind: StaffHubLookupKind) =>
  api.get<StaffLookupAdminRow[]>(`${staffHubLookupsBase}/${kind}`);

export const createStaffHubLookup = (kind: StaffHubLookupKind, payload: StaffLookupWritePayload) =>
  api.post<{ id: number }>(`${staffHubLookupsBase}/${kind}`, payload);

export const updateStaffHubLookup = (kind: StaffHubLookupKind, id: number, payload: StaffLookupWritePayload) =>
  api.put(`${staffHubLookupsBase}/${kind}/${id}`, payload);

export const deleteStaffHubLookup = (kind: StaffHubLookupKind, id: number) =>
  api.delete(`${staffHubLookupsBase}/${kind}/${id}`);
