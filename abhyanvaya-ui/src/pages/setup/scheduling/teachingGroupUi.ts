import {
  TeachingGroupActivityKind,
  TeachingGroupMembershipSource,
  TeachingGroupStatus,
  TeachingGroupType,
} from "../../../services/teachingGroupService";

export const teachingGroupTypeLabel = (value: number): string => {
  switch (value) {
    case TeachingGroupType.SectionDerived:
      return "Section-derived";
    case TeachingGroupType.CombinedSections:
      return "Combined sections";
    case TeachingGroupType.StudentSubset:
      return "Student subset";
    case TeachingGroupType.Elective:
      return "Elective";
    case TeachingGroupType.Laboratory:
      return "Laboratory";
    case TeachingGroupType.CapacitySplit:
      return "Capacity split";
    case TeachingGroupType.Custom:
      return "Custom";
    default:
      return `Type ${value}`;
  }
};

export const teachingGroupStatusLabel = (value: number): string => {
  switch (value) {
    case TeachingGroupStatus.Draft:
      return "Draft";
    case TeachingGroupStatus.Active:
      return "Active";
    case TeachingGroupStatus.Locked:
      return "Locked";
    case TeachingGroupStatus.Archived:
      return "Archived";
    default:
      return `Status ${value}`;
  }
};

export const teachingGroupMembershipSourceLabel = (value: number): string => {
  switch (value) {
    case TeachingGroupMembershipSource.Section:
      return "Section";
    case TeachingGroupMembershipSource.CombinedSections:
      return "Combined sections";
    case TeachingGroupMembershipSource.StudentSubject:
      return "Student–subject";
    case TeachingGroupMembershipSource.ExplicitStudents:
      return "Explicit students";
    case TeachingGroupMembershipSource.Hybrid:
      return "Hybrid";
    default:
      return `Source ${value}`;
  }
};

export const teachingGroupActivityKindLabel = (value: number): string => {
  switch (value) {
    case TeachingGroupActivityKind.Lecture:
      return "Lecture";
    case TeachingGroupActivityKind.Laboratory:
      return "Laboratory";
    case TeachingGroupActivityKind.Tutorial:
      return "Tutorial";
    case TeachingGroupActivityKind.Seminar:
      return "Seminar";
    case TeachingGroupActivityKind.Other:
      return "Other";
    default:
      return `Activity ${value}`;
  }
};

/** Client usability only — server remains authoritative. */
export const parseOptionalCapacity = (raw: string): number | null => {
  const trimmed = raw.trim();
  if (!trimmed) return null;
  const n = Number(trimmed);
  if (!Number.isFinite(n) || !Number.isInteger(n)) return NaN;
  return n;
};

export const formatCapacityDisplay = (value: number | null | undefined): string =>
  value == null ? "—" : String(value);

/** Informational only — never auto-clear or block selection by itself. */
export const isResolvedOverMaxTeachingCapacity = (
  resolvedStudentCount: number,
  maxTeachingCapacity: number | null | undefined,
): boolean =>
  maxTeachingCapacity != null &&
  maxTeachingCapacity > 0 &&
  resolvedStudentCount > maxTeachingCapacity;

export type TeachingGroupSelectorOptionLike = {
  id: number;
  code?: string | null;
  name: string;
  type: number;
  status: number;
  resolvedStudentCount: number;
  expectedStudentCount?: number | null;
  maxTeachingCapacity?: number | null;
  isAssignedToEntry?: boolean;
};

/** Display line for timetable TG selector MenuItem (no client compatibility logic). */
export const formatTeachingGroupSelectorOptionLabel = (option: TeachingGroupSelectorOptionLike): string => {
  const code = option.code?.trim();
  const head = code ? `${code} — ${option.name}` : option.name;
  const type = teachingGroupTypeLabel(option.type);
  const students = `Students: ${option.resolvedStudentCount}`;
  const expected =
    option.expectedStudentCount == null ? null : `Expected: ${option.expectedStudentCount}`;
  const max =
    option.maxTeachingCapacity == null ? null : `Max: ${option.maxTeachingCapacity}`;
  const status =
    option.status === TeachingGroupStatus.Archived ? "Archived — currently assigned" : null;
  return [head, `Type: ${type}`, students, expected, max, status].filter(Boolean).join(" · ");
};

/**
 * Guard helper: UI must never invent a Teaching Group from SubjectAllocation alone.
 * Returns false always — present for architecture tests / documentation.
 */
export const shouldAutoCreateTeachingGroupFromSubjectAllocation = (): boolean => false;
