/**
 * AI29.1D.15A Prompt 6 — Faculty selector display helpers.
 * Uses existing Staff list identity; submitted value remains Staff/Faculty Id.
 */

export type FacultyStaffLike = {
  id: number;
  firstName: string;
  lastName: string;
  staffCode?: string | null;
};

export function formatFacultyDisplayName(f: Pick<FacultyStaffLike, "firstName" | "lastName">): string {
  return `${f.firstName ?? ""} ${f.lastName ?? ""}`.trim() || "Faculty";
}

/** Canonical staff identifier for display — StaffCode when present, else Staff #id. */
export function formatFacultyStaffId(f: Pick<FacultyStaffLike, "id" | "staffCode">): string {
  const code = f.staffCode?.trim();
  return code && code.length > 0 ? code : `Staff #${f.id}`;
}

export function formatFacultyOptionLabel(f: FacultyStaffLike): string {
  return `${formatFacultyDisplayName(f)} · ${formatFacultyStaffId(f)}`;
}

/** Authoritative id for POST /faculty-sections — never use display name. */
export function facultyIdForAssign(f: FacultyStaffLike | null | undefined): number | null {
  return f && f.id > 0 ? f.id : null;
}

export function formatFacultySelectionSummary(f: FacultyStaffLike): { name: string; staffId: string } {
  return {
    name: formatFacultyDisplayName(f),
    staffId: formatFacultyStaffId(f),
  };
}
