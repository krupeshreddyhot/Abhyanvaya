import api from "../api/axios";

export type CourseDto = { id: number; code: string; name: string };
export type GroupDto = { id: number; code: string; name: string; courseId: number };
export type SemesterDto = { id: number; name: string };
export type SubjectDto = { id: number; code: string | null; name: string; isElective: boolean };

export type AttendanceStudentDto = {
  slNo: number;
  studentNumber: string;
  batch: number | null;
  name: string;
  mobileNumber: string | null;
  alternateMobileNumber: string | null;
  mobile: string;
  email: string | null;
  status: number;
  /** Prompt 13 additive — underlying StudentSections membership for reporting. */
  sectionId?: number | null;
  sectionCode?: string | null;
};

export type StudentsForMarkingResponse = {
  isLocked: boolean;
  alreadyMarked: boolean;
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  students: AttendanceStudentDto[];
  /** Prompt 13 additive — TimetableSections / multi-select operational class. */
  isCombinedClass?: boolean;
  participatingSectionIds?: number[];
  participatingSectionCodes?: string[];
  operationalClassLabel?: string | null;
};

export const getCourses = async () => api.get<CourseDto[]>("/master/courses");
export const getSemesters = async () => api.get<SemesterDto[]>("/master/semesters");
export const getGroups = async (courseId?: number) =>
  api.get<GroupDto[]>("/master/groups", { params: courseId ? { courseId } : undefined });
export const getSubjects = async (
  courseId: number,
  groupId: number,
  semesterId: number,
  config?: { signal?: AbortSignal },
) =>
  api.get<SubjectDto[]>("/master/subjects", {
    params: { courseId, groupId, semesterId },
    signal: config?.signal,
  });

export const getStudentsForMarking = async (
  params: {
    courseId: number;
    groupId: number;
    semesterId: number;
    subjectId: number;
    date: string;
    search?: string;
    pageNumber?: number;
    pageSize?: number;
    /** AI29 optional — omit for legacy full Course/Group/Semester cohort. */
    sectionId?: number;
    /** AI29 optional — combined sections (A+B → one roster). */
    sectionIds?: number[];
  },
  config?: { signal?: AbortSignal },
) =>
  api.get<StudentsForMarkingResponse>("/attendance/students-for-marking", {
    params: {
      courseId: params.courseId,
      groupId: params.groupId,
      semesterId: params.semesterId,
      subjectId: params.subjectId,
      date: params.date,
      search: params.search,
      pageNumber: params.pageNumber,
      pageSize: params.pageSize,
      ...(params.sectionId != null && params.sectionId > 0 ? { sectionId: params.sectionId } : {}),
      ...(params.sectionIds && params.sectionIds.length > 0 ? { sectionIds: params.sectionIds } : {}),
    },
    signal: config?.signal,
  });

export type AttendanceSavePayload = {
  subjectId: number;
  date: string;
  students: { studentNumber: string; status: number }[];
  /** AI29.1D.15A Prompt 2 — optional; omit for legacy full cohort. */
  sectionId?: number;
  /** AI29.1D.15A Prompt 2 — optional; one = single section, many = combined. */
  sectionIds?: number[];
};

export const markAttendance = async (payload: AttendanceSavePayload) =>
  api.post("/attendance/mark", payload);

export const editAttendance = async (payload: AttendanceSavePayload) =>
  api.put("/attendance/edit", payload);

