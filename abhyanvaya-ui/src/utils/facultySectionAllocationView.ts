/**
 * AI29.1D Prompt 14 / 15A.8 — Faculty allocation display projection for Section management.
 * Composes existing FacultySectionAssignment + SubjectAllocation + SectionGroup APIs.
 * Does not invent a second faculty↔section / combined-section relationship.
 */

export type FacultySectionLike = {
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

export type SubjectAllocationLike = {
  staffId: number;
  subjectId: number;
  academicYearId: number;
  courseId: number;
  groupId: number;
  semesterId: number;
};

export type SectionGroupLike = {
  id: number;
  groupCode: string;
  groupName: string;
  currentSectionIds: number[];
  academicYearId: number;
  courseId?: number;
  groupId?: number;
  semesterId?: number;
};

export type SectionScopeLike = {
  id: number;
  sectionCode: string;
  courseId: number;
  groupId: number;
  semesterId: number;
  academicYearId: number;
};

export type FacultyAllocationStatus = "Current" | "Ended" | "Inactive";

export type FacultySectionAllocationRow = {
  /** Stable UI key (may represent a combined SectionGroup display). */
  key: string;
  /** Persistent FacultySectionAssignment ids (never collapsed into a new DB relationship). */
  assignmentIds: number[];
  sectionIds: number[];
  /** Single: "A" — Combined membership codes: "A + B". */
  sectionLabel: string;
  /** Operational class title: "A" or "Combined · A + B". */
  operationalClassLabel: string;
  /** Underlying section codes for detail (e.g. ["A","B"]). */
  underlyingSectionCodes: string[];
  facultyId: number;
  facultyName: string;
  /** Subject names from matching SubjectAllocations (scheduling); empty if none. */
  subjectLabel: string;
  effectiveFrom: string;
  effectiveTo: string | null;
  allocationStatus: FacultyAllocationStatus;
  role: string;
  isCombinedSectionGroup: boolean;
  sectionGroupCode?: string | null;
};

export function formatOperationalClassLabel(isCombined: boolean, sectionLabel: string): string {
  const label = sectionLabel.trim();
  return isCombined ? `Combined · ${label}` : label;
}

export function formatUnderlyingSectionCodes(codes: readonly string[]): string[] {
  return [...new Set(codes.map((c) => c.trim()).filter(Boolean))].sort((a, b) => a.localeCompare(b));
}

const dayKey = (value?: string | null) => {
  if (!value) return null;
  return value.length >= 10 ? value.slice(0, 10) : value;
};

export function resolveFacultyAllocationStatus(
  row: Pick<FacultySectionLike, "isCurrent" | "effectiveTo">,
  todayIso = new Date().toISOString().slice(0, 10),
): FacultyAllocationStatus {
  if (!row.isCurrent) return "Inactive";
  const to = dayKey(row.effectiveTo);
  if (to && to < todayIso) return "Ended";
  return "Current";
}

export function matchSubjectNamesForFacultySection(input: {
  facultyId: number;
  section: SectionScopeLike;
  allocations: readonly SubjectAllocationLike[];
  subjectNameById: ReadonlyMap<number, string>;
}): string[] {
  const names = input.allocations
    .filter(
      (a) =>
        a.staffId === input.facultyId &&
        a.academicYearId === input.section.academicYearId &&
        a.courseId === input.section.courseId &&
        a.groupId === input.section.groupId &&
        a.semesterId === input.section.semesterId,
    )
    .map((a) => input.subjectNameById.get(a.subjectId) ?? `Subject #${a.subjectId}`);
  return [...new Set(names)].sort((a, b) => a.localeCompare(b));
}

function findSectionGroupForSections(
  sectionIds: readonly number[],
  groups: readonly SectionGroupLike[],
): SectionGroupLike | null {
  const set = new Set(sectionIds);
  if (set.size === 0) return null;
  return (
    groups.find((g) => {
      const members = g.currentSectionIds ?? [];
      if (members.length < 2) return false;
      // Faculty teaches a combined group when assigned to 2+ of its members.
      const overlap = members.filter((id) => set.has(id));
      return overlap.length >= 2;
    }) ?? null
  );
}

/**
 * Build display rows: one row per faculty-section assignment, with Subject enrichment.
 * When the same faculty has multiple current assignments that belong to one SectionGroup,
 * collapse those into a single combined operational row (underlying assignment ids retained).
 */
export function buildFacultySectionAllocationRows(input: {
  assignments: readonly FacultySectionLike[];
  sections: readonly SectionScopeLike[];
  subjectAllocations: readonly SubjectAllocationLike[];
  subjectNameById: ReadonlyMap<number, string>;
  sectionGroups: readonly SectionGroupLike[];
  sectionFilterId?: number;
  todayIso?: string;
}): FacultySectionAllocationRow[] {
  const sectionById = new Map(input.sections.map((s) => [s.id, s]));
  let assignments = input.assignments.filter((a) => sectionById.has(a.sectionId));
  if (input.sectionFilterId && input.sectionFilterId > 0) {
    assignments = assignments.filter((a) => a.sectionId === input.sectionFilterId);
  }

  const today = input.todayIso ?? new Date().toISOString().slice(0, 10);
  const consumed = new Set<number>();
  const rows: FacultySectionAllocationRow[] = [];

  // Prefer collapsing current assignments by faculty + overlapping SectionGroup.
  const byFaculty = new Map<number, FacultySectionLike[]>();
  for (const a of assignments) {
    const list = byFaculty.get(a.facultyId) ?? [];
    list.push(a);
    byFaculty.set(a.facultyId, list);
  }

  for (const [facultyId, facultyAssignments] of byFaculty) {
    const current = facultyAssignments.filter((a) => a.isCurrent);
    const group = findSectionGroupForSections(
      current.map((a) => a.sectionId),
      input.sectionGroups,
    );

    if (group && current.length >= 2) {
      const memberSet = new Set(group.currentSectionIds);
      const inGroup = current.filter((a) => memberSet.has(a.sectionId));
      if (inGroup.length >= 2) {
        const codes = inGroup
          .map((a) => a.sectionCode || sectionById.get(a.sectionId)?.sectionCode || `#${a.sectionId}`)
          .sort((a, b) => a.localeCompare(b));
        const subjectNames = new Set<string>();
        for (const a of inGroup) {
          const section = sectionById.get(a.sectionId);
          if (!section) continue;
          for (const name of matchSubjectNamesForFacultySection({
            facultyId,
            section,
            allocations: input.subjectAllocations,
            subjectNameById: input.subjectNameById,
          })) {
            subjectNames.add(name);
          }
        }
        const earliestFrom = [...inGroup]
          .map((a) => dayKey(a.effectiveFrom) ?? a.effectiveFrom)
          .sort()[0]!;
        const openEnded = inGroup.some((a) => !a.effectiveTo);
        const latestTo = openEnded
          ? null
          : [...inGroup]
              .map((a) => dayKey(a.effectiveTo)!)
              .filter(Boolean)
              .sort()
              .at(-1) ?? null;

        const sectionLabel = codes.join(" + ");
        const underlying = formatUnderlyingSectionCodes(codes);
        rows.push({
          key: `combined-${facultyId}-${group.id}`,
          assignmentIds: inGroup.map((a) => a.id),
          sectionIds: inGroup.map((a) => a.sectionId),
          sectionLabel,
          operationalClassLabel: formatOperationalClassLabel(true, sectionLabel),
          underlyingSectionCodes: underlying,
          facultyId,
          facultyName: inGroup[0]?.facultyName?.trim() || `Faculty #${facultyId}`,
          subjectLabel: [...subjectNames].sort((a, b) => a.localeCompare(b)).join(", ") || "—",
          effectiveFrom: earliestFrom,
          effectiveTo: latestTo,
          allocationStatus: resolveFacultyAllocationStatus(
            { isCurrent: true, effectiveTo: latestTo },
            today,
          ),
          role: inGroup.some((a) => a.role === "Primary") ? "Primary" : inGroup[0]!.role,
          isCombinedSectionGroup: true,
          sectionGroupCode: group.groupCode,
        });
        for (const a of inGroup) consumed.add(a.id);
      }
    }
  }

  for (const a of assignments) {
    if (consumed.has(a.id)) continue;
    const section = sectionById.get(a.sectionId);
    const subjects = section
      ? matchSubjectNamesForFacultySection({
          facultyId: a.facultyId,
          section,
          allocations: input.subjectAllocations,
          subjectNameById: input.subjectNameById,
        })
      : [];
    const code = a.sectionCode || section?.sectionCode || `#${a.sectionId}`;
    const underlying = formatUnderlyingSectionCodes([code]);
    rows.push({
      key: `assign-${a.id}`,
      assignmentIds: [a.id],
      sectionIds: [a.sectionId],
      sectionLabel: code,
      operationalClassLabel: formatOperationalClassLabel(false, code),
      underlyingSectionCodes: underlying,
      facultyId: a.facultyId,
      facultyName: a.facultyName?.trim() || `Faculty #${a.facultyId}`,
      subjectLabel: subjects.join(", ") || "—",
      effectiveFrom: dayKey(a.effectiveFrom) ?? a.effectiveFrom,
      effectiveTo: dayKey(a.effectiveTo),
      allocationStatus: resolveFacultyAllocationStatus(a, today),
      role: a.role,
      isCombinedSectionGroup: false,
      sectionGroupCode: null,
    });
  }

  return rows.sort((a, b) => {
    const sec = a.sectionLabel.localeCompare(b.sectionLabel);
    if (sec !== 0) return sec;
    return a.facultyName.localeCompare(b.facultyName);
  });
}
