import api from "../api/axios";

export type SectionDto = {
  id: number;
  collegeId: number;
  academicYearId: number;
  academicYearName?: string | null;
  courseId: number;
  courseName?: string | null;
  groupId: number;
  groupName?: string | null;
  semesterId: number;
  semesterName?: string | null;
  sectionCode: string;
  sectionName: string;
  displayOrder: number;
  maximumStrength: number;
  status: string;
  currentStrength: number;
  remainingCapacity: number;
};

export type StudentSectionDto = {
  id: number;
  studentId: number;
  studentNumber?: string | null;
  studentName?: string | null;
  sectionId: number;
  sectionCode?: string | null;
  sectionName?: string | null;
  effectiveFrom: string;
  effectiveTo?: string | null;
  isCurrent: boolean;
  transferReason?: string | null;
};

export type FacultySectionDto = {
  id: number;
  facultyId: number;
  facultyName?: string | null;
  sectionId: number;
  sectionCode?: string | null;
  sectionName?: string | null;
  academicYearId: number;
  role: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  isCurrent: boolean;
};

export type TimetableSectionDto = {
  id: number;
  timetableId: number;
  timetableEntryId?: number | null;
  sectionId: number;
  sectionCode?: string | null;
  sectionName?: string | null;
};

export type SectionStatisticsDto = {
  sectionId: number;
  sectionCode: string;
  sectionName: string;
  maximumStrength: number;
  studentCount: number;
  facultyCount: number;
  remainingCapacity: number;
  utilizationPercent: number;
};

const base = "/sections";

export const listSections = (params?: {
  academicYearId?: number;
  courseId?: number;
  groupId?: number;
  semesterId?: number;
}) => api.get<SectionDto[]>(base, { params });

export const getSection = (id: number) => api.get<SectionDto>(`${base}/${id}`);

export const createSection = (body: {
  academicYearId: number;
  courseId: number;
  groupId: number;
  semesterId: number;
  sectionCode: string;
  sectionName: string;
  displayOrder?: number;
  maximumStrength?: number;
  status?: string;
}) => api.post<SectionDto>(base, body);

export const updateSection = (
  id: number,
  body: {
    sectionCode: string;
    sectionName: string;
    displayOrder: number;
    maximumStrength: number;
    status: string;
  },
) => api.put<SectionDto>(`${base}/${id}`, body);

export const deleteSection = (id: number) => api.delete(`${base}/${id}`);

export const listStudentSections = (params?: { sectionId?: number; studentId?: number; currentOnly?: boolean }) =>
  api.get<StudentSectionDto[]>("/student-sections", { params });

export const assignStudentSection = (body: { studentId: number; sectionId: number; effectiveFrom?: string }) =>
  api.post<StudentSectionDto>("/student-sections", body);

export const transferStudentSection = (body: {
  studentId: number;
  targetSectionId: number;
  effectiveFrom?: string;
  reason?: string;
}) => api.post<StudentSectionDto>("/student-sections/transfer", body);

export const listFacultySections = (params?: { sectionId?: number; facultyId?: number; currentOnly?: boolean }) =>
  api.get<FacultySectionDto[]>("/faculty-sections", { params });

export const assignFacultySection = (body: {
  facultyId: number;
  sectionId: number;
  academicYearId: number;
  role?: string;
  effectiveFrom?: string;
}) => api.post<FacultySectionDto>("/faculty-sections", body);

export const listTimetableSections = (timetableId: number) =>
  api.get<TimetableSectionDto[]>(`/timetable/${timetableId}/sections`);

export const setTimetableSections = (
  timetableId: number,
  body: { timetableEntryId?: number | null; sectionIds: number[] },
) => api.put<TimetableSectionDto[]>(`/timetable/${timetableId}/sections`, body);

export const autoAllocateSections = (body: {
  academicYearId: number;
  courseId: number;
  groupId: number;
  semesterId: number;
  strategy?: string;
}) => api.post<{ assignedCount: number; skippedCount: number; strategy: string; messages: string[] }>(`${base}/auto-allocate`, body);

export const getSectionStatistics = (params?: { academicYearId?: number; semesterId?: number }) =>
  api.get<SectionStatisticsDto[]>(`${base}/statistics`, { params });
