import {
  FacultyAvailabilityType,
  PreferredTeachingMode,
  RoomAvailabilityType,
  RoomType,
  TimeSlotTemplateType,
} from "../../../services/schedulingService";

export const FACULTY_AVAILABILITY_LABELS: Record<number, string> = {
  [FacultyAvailabilityType.Preferred]: "Preferred",
  [FacultyAvailabilityType.Unavailable]: "Unavailable",
  [FacultyAvailabilityType.AdministrativeDuty]: "Administrative duty",
  [FacultyAvailabilityType.ExamDuty]: "Exam duty",
  [FacultyAvailabilityType.ApprovedLeave]: "Approved leave",
  [FacultyAvailabilityType.Custom]: "Custom",
};

export const FACULTY_AVAILABILITY_COLORS: Record<number, string> = {
  [FacultyAvailabilityType.Preferred]: "#2e7d32",
  [FacultyAvailabilityType.Unavailable]: "#d32f2f",
  [FacultyAvailabilityType.AdministrativeDuty]: "#ed6c02",
  [FacultyAvailabilityType.ExamDuty]: "#9c27b0",
  [FacultyAvailabilityType.ApprovedLeave]: "#0288d1",
  [FacultyAvailabilityType.Custom]: "#616161",
};

export const ROOM_AVAILABILITY_LABELS: Record<number, string> = {
  [RoomAvailabilityType.Available]: "Available",
  [RoomAvailabilityType.Maintenance]: "Maintenance",
  [RoomAvailabilityType.Reserved]: "Reserved",
  [RoomAvailabilityType.Examination]: "Examination",
  [RoomAvailabilityType.Blocked]: "Blocked",
  [RoomAvailabilityType.Cleaning]: "Cleaning",
};

export const ROOM_AVAILABILITY_COLORS: Record<number, string> = {
  [RoomAvailabilityType.Available]: "#2e7d32",
  [RoomAvailabilityType.Maintenance]: "#ed6c02",
  [RoomAvailabilityType.Reserved]: "#0288d1",
  [RoomAvailabilityType.Examination]: "#9c27b0",
  [RoomAvailabilityType.Blocked]: "#d32f2f",
  [RoomAvailabilityType.Cleaning]: "#616161",
};

export const ROOM_TYPE_LABELS: Record<number, string> = {
  [RoomType.Classroom]: "Classroom",
  [RoomType.ComputerLab]: "Computer lab",
  [RoomType.ScienceLab]: "Science lab",
  [RoomType.CommerceLab]: "Commerce lab",
  [RoomType.Seminar]: "Seminar",
  [RoomType.Auditorium]: "Auditorium",
  [RoomType.Other]: "Other",
};

export const TEMPLATE_TYPE_LABELS: Record<number, string> = {
  [TimeSlotTemplateType.Regular]: "Regular",
  [TimeSlotTemplateType.Friday]: "Friday",
  [TimeSlotTemplateType.HalfDay]: "Half day",
  [TimeSlotTemplateType.Examination]: "Examination",
  [TimeSlotTemplateType.Holiday]: "Holiday",
  [TimeSlotTemplateType.Summer]: "Summer",
  [TimeSlotTemplateType.Winter]: "Winter",
};

export const PREFERRED_TEACHING_MODE_LABELS: Record<number, string> = {
  [PreferredTeachingMode.Morning]: "Morning",
  [PreferredTeachingMode.Afternoon]: "Afternoon",
  [PreferredTeachingMode.Evening]: "Evening",
  [PreferredTeachingMode.Any]: "Any",
};
