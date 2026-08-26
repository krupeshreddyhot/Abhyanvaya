import type { AttendanceContext } from "./attendanceContext";
import type { ProgramDto } from "../services/programService";
import type { AcademicYearDto } from "../services/schedulingService";
import type { CourseRow, GroupRow, SemesterRow, StaffListItem } from "../services/setupService";
import type { SectionDto } from "../services/sectionService";
import type { SubjectDto } from "../services/attendanceService";

/**
 * Canonical operational academic selection for AI29.1D.
 * Subject Master remains Course + Group + Semester (Section never scopes subjects).
 */
export type AcademicUiSelection = {
  academicYearId: number | null;
  /** Optional — only meaningful when tenant EnablePrograms is true. */
  programId: number | null;
  courseId: number | null;
  groupId: number | null;
  semesterId: number | null;
  /** Optional operational grouping — never part of Subject Master. */
  sectionId: number | null;
  /** Combined-class multi-select (TimetableSections / SectionGroup consumers). */
  sectionIds: number[];
  subjectId: number | null;
  facultyId: number | null;
};

/** Soft timetable resolution overlay — mirrors attendance-resolution contract (additive). */
export type AcademicTimetableContext = {
  mode?: string | null;
  hasTimetable?: boolean;
  message?: string | null;
  timetableId?: number | null;
  timetableEntryId?: number | null;
  courseId?: number | null;
  groupId?: number | null;
  semesterId?: number | null;
  subjectId?: number | null;
  periodNumber?: number | null;
  timeSlotId?: number | null;
  roomId?: number | null;
  subjectName?: string | null;
  roomName?: string | null;
  attendanceDate?: string | null;
  /** Additive from AttendanceSessionResolver TimetableSections enrichment. */
  sectionIds?: number[] | null;
  sectionCodes?: string[] | null;
};

/** Attendance marking overlay — extends existing AttendanceContext without requiring Section. */
export type AcademicAttendanceContext = AttendanceContext & {
  academicYearId?: number | null;
  programId?: number | null;
  sectionId?: number | null;
  sectionIds?: number[];
  facultyId?: number | null;
};

export type AcademicUiCatalogs = {
  academicYears: AcademicYearDto[];
  programs: ProgramDto[];
  courses: CourseRow[];
  groups: GroupRow[];
  semesters: SemesterRow[];
  sections: SectionDto[];
  subjects: SubjectDto[];
  /** Lightweight faculty options (paginated loads only; never full dump). */
  faculty: StaffListItem[];
};

export type AcademicUiFilteredOptions = {
  programs: ProgramDto[];
  courses: CourseRow[];
  groups: GroupRow[];
  semesters: SemesterRow[];
  sections: SectionDto[];
  subjects: SubjectDto[];
};

export type AcademicHierarchyLevel =
  | "academicYear"
  | "program"
  | "course"
  | "group"
  | "semester"
  | "section"
  | "subject"
  | "faculty";

export const emptyAcademicUiSelection = (): AcademicUiSelection => ({
  academicYearId: null,
  programId: null,
  courseId: null,
  groupId: null,
  semesterId: null,
  sectionId: null,
  sectionIds: [],
  subjectId: null,
  facultyId: null,
});

export const emptyAcademicUiCatalogs = (): AcademicUiCatalogs => ({
  academicYears: [],
  programs: [],
  courses: [],
  groups: [],
  semesters: [],
  sections: [],
  subjects: [],
  faculty: [],
});
