import api from "../api/axios";

export type ProgramDto = {
  id: number;
  collegeId: number;
  departmentId: number;
  departmentCode?: string | null;
  departmentName?: string | null;
  programCode: string;
  programName: string;
  description?: string | null;
  displayOrder: number;
  isActive: boolean;
  status: string;
  icon?: string | null;
  themeColor?: string | null;
  academicCalendarId?: number | null;
  courseCount: number;
  studentCount: number;
  facultyCount: number;
};

export type ProgramDepartmentOptionDto = {
  id: number;
  collegeId: number;
  name: string;
  code?: string | null;
  isActive: boolean;
};

export type TenantAcademicConfigurationDto = {
  id: number;
  collegeId: number;
  enablePrograms: boolean;
};

export type AcademicHierarchyDto = {
  enablePrograms: boolean;
  roots: AcademicHierarchyNodeDto[];
};

export type AcademicHierarchyNodeDto = {
  kind: string;
  id: number;
  code: string;
  name: string;
  isActive?: boolean;
  children?: AcademicHierarchyNodeDto[];
};

export const listPrograms = (includeInactive = false) =>
  api.get<ProgramDto[]>("/programs", { params: { includeInactive } });

export const getProgram = (id: number) => api.get<ProgramDto>(`/programs/${id}`);

export const listProgramDepartmentOptions = () =>
  api.get<ProgramDepartmentOptionDto[]>("/programs/department-options");

export const createProgram = (body: {
  departmentId: number;
  programCode: string;
  programName: string;
  description?: string;
  displayOrder?: number;
  isActive?: boolean;
}) => api.post<ProgramDto>("/programs", body);

export const updateProgram = (
  id: number,
  body: {
    departmentId: number;
    programCode: string;
    programName: string;
    description?: string;
    displayOrder: number;
    isActive: boolean;
    status: string;
  },
) => api.put<ProgramDto>(`/programs/${id}`, body);

export const archiveProgram = (id: number) => api.post(`/programs/${id}/archive`);
export const deleteProgram = (id: number) => api.delete(`/programs/${id}`);

export const getAcademicConfiguration = () =>
  api.get<TenantAcademicConfigurationDto>("/academic-structure/configuration");

export const updateAcademicConfiguration = (enablePrograms: boolean) =>
  api.put<TenantAcademicConfigurationDto>("/academic-structure/configuration", { enablePrograms });

export const getAcademicHierarchy = (params?: {
  includeInactive?: boolean;
  includeSections?: boolean;
  includeSubjects?: boolean;
}) => api.get<AcademicHierarchyDto>("/academic-structure", { params });

export const assignCourseToProgram = (courseId: number, programId: number | null) =>
  api.post("/programs/assign-course", { courseId, programId });

/** Courses with Course.ProgramId == programId (authoritative relationship). */
export type ProgramCourseRow = {
  id: number;
  code: string;
  name: string;
  programId?: number | null;
  displayOrder?: number;
};

export const getProgramCourses = (programId: number) =>
  api.get<ProgramCourseRow[]>(`/programs/${programId}/courses`);

export const getProgramCourseCount = (programId: number) =>
  api.get<number>(`/programs/${programId}/course-count`);
