import {
  TeachingGroupMemberProvenance,
  TeachingGroupMembershipInclusion,
  TeachingGroupMembershipSource,
  type ResolvedTeachingGroupMemberDto,
  type TeachingGroupMembershipDto,
} from "../../../services/teachingGroupService";

/** Safe UX copy for HTTP 409 membership concurrency (server remains authoritative). */
export const MEMBERSHIP_CONFLICT_MESSAGE =
  "The Teaching Group membership was changed by another user. The latest membership has been loaded. Please review the changes before trying again.";

export const isMutableMembershipSource = (source: number): boolean =>
  source === TeachingGroupMembershipSource.ExplicitStudents ||
  source === TeachingGroupMembershipSource.Hybrid;

export const isExplicitStudentsSource = (source: number): boolean =>
  source === TeachingGroupMembershipSource.ExplicitStudents;

export const isHybridSource = (source: number): boolean =>
  source === TeachingGroupMembershipSource.Hybrid;

export const currentIncludeOverlays = (
  memberships: TeachingGroupMembershipDto[],
): TeachingGroupMembershipDto[] =>
  memberships.filter(
    (m) =>
      m.isCurrent &&
      (m.inclusion === TeachingGroupMembershipInclusion.Include || m.inclusion === 1),
  );

export const currentExcludeOverlays = (
  memberships: TeachingGroupMembershipDto[],
): TeachingGroupMembershipDto[] =>
  memberships.filter(
    (m) =>
      m.isCurrent &&
      (m.inclusion === TeachingGroupMembershipInclusion.Exclude || m.inclusion === 2),
  );

/** Derived = server Provenance.Derived; never invent from StudentSection locally. */
export const derivedResolvedMembers = (
  resolved: ResolvedTeachingGroupMemberDto[],
): ResolvedTeachingGroupMemberDto[] =>
  resolved.filter(
    (r) =>
      r.provenance === TeachingGroupMemberProvenance.Derived || r.provenance === 1,
  );

export const explicitIncludeResolvedMembers = (
  resolved: ResolvedTeachingGroupMemberDto[],
): ResolvedTeachingGroupMemberDto[] =>
  resolved.filter(
    (r) =>
      r.provenance === TeachingGroupMemberProvenance.ExplicitInclude || r.provenance === 2,
  );

export const teachingGroupMemberProvenanceLabel = (provenance: number): string => {
  switch (provenance) {
    case TeachingGroupMemberProvenance.Derived:
      return "Derived";
    case TeachingGroupMemberProvenance.ExplicitInclude:
      return "Explicit include";
    default:
      return `Provenance ${provenance}`;
  }
};

/**
 * Informational only — never auto-remove students or create groups.
 * Empty / zero resolved is allowed by domain.
 */
export const isResolvedOverMaxCapacity = (
  resolvedStudentCount: number,
  maxTeachingCapacity: number | null | undefined,
): boolean =>
  maxTeachingCapacity != null &&
  maxTeachingCapacity > 0 &&
  resolvedStudentCount > maxTeachingCapacity;

/** Deduplicate while preserving first-seen order. */
export const uniqueStudentIds = (ids: number[]): number[] => {
  const seen = new Set<number>();
  const out: number[] = [];
  for (const id of ids) {
    if (!Number.isFinite(id) || seen.has(id)) continue;
    seen.add(id);
    out.push(id);
  }
  return out;
};

export type StudentDisplayHint = {
  id: number;
  studentNumber?: string | null;
  name?: string | null;
  courseName?: string | null;
  groupName?: string | null;
};

export const formatStudentMembershipLabel = (hint: StudentDisplayHint): string => {
  const number = hint.studentNumber?.trim();
  const name = hint.name?.trim();
  if (number && name) return `${number} — ${name}`;
  if (name) return name;
  if (number) return number;
  return `Student #${hint.id}`;
};

export const formatStudentMembershipSecondary = (hint: StudentDisplayHint): string | null => {
  const course = hint.courseName?.trim();
  const group = hint.groupName?.trim();
  if (course && group) return `${course} / ${group}`;
  if (course) return course;
  if (group) return group;
  return null;
};

/**
 * Architecture guard helper — UI must never resolve membership client-side.
 * Always returns false; present for tests / documentation.
 */
export const shouldCalculateResolvedMembershipInUi = (): boolean => false;
