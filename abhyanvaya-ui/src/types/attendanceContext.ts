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
  /** AI29.1D — optional; empty = full Course/Group/Semester cohort. */
  sectionIds?: number[];
  sectionCodes?: string[];
  roomName?: string;
  /** Timetable = resolver prefilled; Manual = no timetable assignment required. */
  scopeMode?: "Timetable" | "Manual";
}
