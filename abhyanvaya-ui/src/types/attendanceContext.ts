export type AttendanceMethodMode = "manual" | "aiPhoto";

export interface AttendanceContext {
  courseId: number;
  groupId: number;
  semesterId: number;
  subjectId: number;
  attendanceDate: string;
  periodNumber: number;
  attendanceMethod: AttendanceMethodMode;
  courseName?: string;
  groupName?: string;
  semesterName?: string;
  subjectName?: string;
}
