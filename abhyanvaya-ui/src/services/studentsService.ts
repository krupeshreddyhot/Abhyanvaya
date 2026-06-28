import api from "../api/axios";

export type MasterOptionDto = {
  id: number;
  name: string;
};

export type GroupOptionDto = {
  id: number;
  name: string;
  courseId: number;
};

export type StudentRecordDto = {
  id: number;
  appraId?: string | null;
  studentNumber: string;
  name: string;
  courseId: number;
  courseName: string;
  groupId: number;
  groupName: string;
  semesterId: number;
  semesterName: string;
  genderId: number;
  genderName: string;
  mediumId: number;
  mediumName: string;
  firstLanguageId: number;
  firstLanguageName: string;
  languageId: number;
  languageName: string;
  batch?: number | null;
  dateOfBirth?: string | null;
  mobileNumber?: string | null;
  alternateMobileNumber?: string | null;
  email?: string | null;
  parentMobileNumber?: string | null;
  parentAlternateMobileNumber?: string | null;
  fatherName?: string | null;
  motherName?: string | null;
  photoKey?: string | null;
  photoUploadedUtc?: string | null;
  photoVerified?: boolean;
};

export type StudentPhotoDto = {
  hasPhoto: boolean;
  photoKey?: string | null;
  photoUploadedUtc?: string | null;
  photoVerified: boolean;
  originalUrl?: string | null;
  thumbnailUrl?: string | null;
};

export type StudentPhotoUploadResult = {
  photoKey: string;
  photoUploadedUtc: string;
  photoVerified: boolean;
  originalUrl?: string | null;
  thumbnailUrl?: string | null;
};

export type StudentsListResponse = {
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  data: StudentRecordDto[];
};

export type StudentUpsertPayload = {
  appraId?: string;
  studentNumber: string;
  name: string;
  courseId: number;
  groupId: number;
  semesterId: number;
  genderId: number;
  mediumId: number;
  /** Omit on create to let the server default to English for the tenant. */
  firstLanguageId?: number;
  languageId: number;
  batch?: number | null;
  dateOfBirth?: string | null;
  mobileNumber?: string;
  alternateMobileNumber?: string;
  email?: string;
  parentMobileNumber?: string;
  parentAlternateMobileNumber?: string;
  fatherName?: string;
  motherName?: string;
};

export const getStudents = async (params: {
  search?: string;
  batch?: number;
  courseId?: number;
  groupId?: number;
  semesterId?: number;
  pageNumber?: number;
  pageSize?: number;
}) =>
  api.get<StudentsListResponse>("/student", { params });

export const createStudent = async (payload: StudentUpsertPayload) => api.post("/student", payload);

export const updateStudent = async (payload: StudentUpsertPayload & { id: number }) => api.put("/student", payload);

export const getCourses = async () => api.get<MasterOptionDto[]>("/master/courses");
export const getGroups = async (courseId?: number) =>
  api.get<GroupOptionDto[]>("/master/groups", { params: courseId ? { courseId } : undefined });
export const getSemesters = async () => api.get<MasterOptionDto[]>("/master/semesters");
export const getGenders = async () => api.get<MasterOptionDto[]>("/master/genders");
export const getMediums = async () => api.get<MasterOptionDto[]>("/master/mediums");
export const getLanguages = async () => api.get<MasterOptionDto[]>("/master/languages");

export const exportStudentsCsv = async (params?: {
  search?: string;
  batch?: number;
  courseId?: number;
  groupId?: number;
  semesterId?: number;
}) =>
  api.get<Blob>("/student/export", {
    params,
    responseType: "blob",
  });

export const getStudentPhoto = async (studentId: number) =>
  api.get<StudentPhotoDto>(`/student/${studentId}/photo`);

export const uploadStudentPhoto = async (
  studentId: number,
  file: File,
  onUploadProgress?: (percent: number) => void
) => {
  const form = new FormData();
  form.append("file", file);
  return api.post<StudentPhotoUploadResult>(`/student/${studentId}/photo`, form, {
    onUploadProgress: (event) => {
      if (!event.total) return;
      onUploadProgress?.(Math.round((event.loaded / event.total) * 100));
    },
  });
};

export const deleteStudentPhoto = async (studentId: number) =>
  api.delete(`/student/${studentId}/photo`);

