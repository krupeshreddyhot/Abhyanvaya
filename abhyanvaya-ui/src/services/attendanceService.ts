import api from "../api/axios";

export type CourseDto = { id: number; name: string };
export type GroupDto = { id: number; name: string; courseId: number };
export type SemesterDto = { id: number; name: string };
export type SubjectDto = { id: number; name: string; isElective: boolean };

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
};

export type StudentsForMarkingResponse = {
  isLocked: boolean;
  alreadyMarked: boolean;
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  students: AttendanceStudentDto[];
};

export const getCourses = async () => api.get<CourseDto[]>("/master/courses");
export const getSemesters = async () => api.get<SemesterDto[]>("/master/semesters");
export const getGroups = async (courseId?: number) =>
  api.get<GroupDto[]>("/master/groups", { params: courseId ? { courseId } : undefined });
export const getSubjects = async (courseId: number, groupId: number, semesterId: number) =>
  api.get<SubjectDto[]>("/master/subjects", { params: { courseId, groupId, semesterId } });

export const getStudentsForMarking = async (params: {
  courseId: number;
  groupId: number;
  semesterId: number;
  subjectId: number;
  date: string;
  search?: string;
  pageNumber?: number;
  pageSize?: number;
}) => api.get<StudentsForMarkingResponse>("/attendance/students-for-marking", { params });

export const markAttendance = async (payload: {
  subjectId: number;
  date: string;
  students: { studentNumber: string; status: number }[];
}) => api.post("/attendance/mark", payload);

export const editAttendance = async (payload: {
  subjectId: number;
  date: string;
  students: { studentNumber: string; status: number }[];
}) => api.put("/attendance/edit", payload);

