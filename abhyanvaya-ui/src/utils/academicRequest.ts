/**
 * AI29.1D Prompt 19 — shared request helpers for enterprise-scale academic UI.
 * Prefer AbortSignal + debounce over loading full catalogs into the browser.
 */

/** Create an AbortController and abort any previous controller (cascade race guard). */
export function replaceAbortController(previous: AbortController | null | undefined): AbortController {
  previous?.abort();
  return new AbortController();
}

export function isAbortError(error: unknown): boolean {
  if (!error || typeof error !== "object") return false;
  const name = (error as { name?: string }).name;
  const code = (error as { code?: string }).code;
  return name === "CanceledError" || name === "AbortError" || code === "ERR_CANCELED";
}

/** Debounce a callback; returns a cancel function. */
export function debounceMs(fn: () => void, ms: number): () => void {
  const handle = window.setTimeout(fn, ms);
  return () => window.clearTimeout(handle);
}

/** Enterprise UI page sizes — keep browser payloads bounded. */
export const ACADEMIC_UI_PAGE_SIZES = {
  facultySearch: 25,
  attendanceRosterPage: 50,
  attendanceRosterFetch: 200,
  allocationStudentPreview: 100,
  allocationPreviewRows: 150,
} as const;
