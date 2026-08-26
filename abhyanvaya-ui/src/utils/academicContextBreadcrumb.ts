import type { AcademicUiSelection } from "../types/academicUiContext";
import type { AcademicOperationalContextQuery } from "../services/academicBreadcrumbService";

/**
 * AI29.1D Prompt 16 — map shared AcademicUi selection (or overrides) to breadcrumb API query.
 * Does not invent display names; the Academic Breadcrumb service owns the path.
 */
export function toAcademicContextBreadcrumbQuery(
  selection: Partial<AcademicUiSelection> | null | undefined,
  override?: AcademicOperationalContextQuery | null,
): AcademicOperationalContextQuery {
  const base: AcademicOperationalContextQuery = {
    programId: selection?.programId ?? null,
    courseId: selection?.courseId ?? null,
    groupId: selection?.groupId ?? null,
    semesterId: selection?.semesterId ?? null,
    sectionId: selection?.sectionId ?? null,
    sectionIds: selection?.sectionIds ?? [],
    subjectId: selection?.subjectId ?? null,
  };
  if (!override) return base;
  return {
    programId: override.programId ?? base.programId,
    courseId: override.courseId ?? base.courseId,
    groupId: override.groupId ?? base.groupId,
    semesterId: override.semesterId ?? base.semesterId,
    sectionId: override.sectionId ?? base.sectionId,
    sectionIds: override.sectionIds ?? base.sectionIds,
    subjectId: override.subjectId ?? base.subjectId,
  };
}

export function academicContextQueryKey(q: AcademicOperationalContextQuery): string {
  const ids = (q.sectionIds ?? []).filter((id) => id > 0).slice().sort((a, b) => a - b);
  return [
    q.programId ?? "",
    q.courseId ?? "",
    q.groupId ?? "",
    q.semesterId ?? "",
    q.sectionId ?? "",
    ids.join(","),
    q.subjectId ?? "",
  ].join("|");
}
