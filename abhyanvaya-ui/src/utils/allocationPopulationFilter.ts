/**
 * AI29.1D — Student population filters over Allocation Context students only.
 * Does not mutate the context; returns a new filtered array.
 *
 * AI29.1D.24B.4 — Full Student Number range (ordinal) vs Last 3 Digits range (numeric 000–999).
 */

export type AllocationContextStudent = {
  studentId: number;
  studentNumber?: string | null;
  studentName?: string | null;
  currentSectionId?: number | null;
  currentSectionCode?: string | null;
  genderId?: number | null;
  gender?: string | null;
  languageId?: number | null;
  language?: string | null;
  scholarshipCategory?: string | null;
  minorSubject?: string | null;
  transportRoute?: string | null;
  hostel?: string | null;
  electiveCombination?: string | null;
  merit?: string | null;
};

export const POPULATION_FILTER_MODES = [
  "All",
  "StudentNumberRange",
  "LastThreeDigitsRange",
  "Gender",
  "ScholarshipCategory",
  "MinorSubject",
  "Language",
  "TransportRoute",
  "Hostel",
  "ElectiveCombination",
  "Merit",
] as const;

export type PopulationFilterMode = (typeof POPULATION_FILTER_MODES)[number];

export type PopulationFilterState = {
  mode: PopulationFilterMode;
  fromStudentNumber: string;
  toStudentNumber: string;
  facetValue: string;
};

export const DEFAULT_POPULATION_FILTER: PopulationFilterState = {
  mode: "All",
  fromStudentNumber: "",
  toStudentNumber: "",
  facetValue: "",
};

type FacetMode = Exclude<PopulationFilterMode, "All" | "StudentNumberRange" | "LastThreeDigitsRange">;

const FACET_ACCESSORS: Record<FacetMode, (s: AllocationContextStudent) => string | null | undefined> = {
  Gender: (s) => s.gender,
  ScholarshipCategory: (s) => s.scholarshipCategory,
  MinorSubject: (s) => s.minorSubject,
  Language: (s) => s.language,
  TransportRoute: (s) => s.transportRoute,
  Hostel: (s) => s.hostel,
  ElectiveCombination: (s) => s.electiveCombination,
  Merit: (s) => s.merit,
};

/** Ordinal ignore-case compare — matches engine StudentNumber ordering semantics. */
export function compareStudentNumbers(a: string | null | undefined, b: string | null | undefined): number {
  return StringComparerOrdinalIgnoreCase(normalizeStudentNumber(a), normalizeStudentNumber(b));
}

function normalizeStudentNumber(value: string | null | undefined): string {
  return (value ?? "").trim();
}

function StringComparerOrdinalIgnoreCase(a: string, b: string): number {
  const left = a.toLocaleUpperCase("en-US");
  const right = b.toLocaleUpperCase("en-US");
  if (left < right) return -1;
  if (left > right) return 1;
  return 0;
}

export type RangeValidation = { ok: true } | { ok: false; message: string };

/** Validate From <= To using alphanumeric-aware ordinal comparison (not numeric-only). */
export function validateStudentNumberRange(from: string, to: string): RangeValidation {
  const fromNorm = normalizeStudentNumber(from);
  const toNorm = normalizeStudentNumber(to);
  if (!fromNorm || !toNorm) {
    return { ok: false, message: "Enter both From and To student numbers." };
  }
  if (compareStudentNumbers(fromNorm, toNorm) > 0) {
    return { ok: false, message: "From Student Number must be less than or equal to To Student Number." };
  }
  return { ok: true };
}

/** Parse Last 3 Digits bound (000–999); normalize to D3. Rejects non-digits. */
export function tryParseLastThreeDigitsBound(raw: string): { ok: true; value: number; normalized: string } | { ok: false; message: string } {
  const trimmed = (raw ?? "").trim();
  if (!trimmed) {
    return { ok: false, message: "Last 3 Digits range requires both From and To (000–999)." };
  }
  if (!/^\d+$/.test(trimmed)) {
    return { ok: false, message: `Invalid Last 3 Digits value '${raw}'. Use digits only (000–999).` };
  }
  const value = Number.parseInt(trimmed, 10);
  if (!Number.isFinite(value) || value < 0 || value > 999) {
    return { ok: false, message: `Last 3 Digits value '${raw}' must be between 000 and 999.` };
  }
  return { ok: true, value, normalized: String(value).padStart(3, "0") };
}

export function validateLastThreeDigitsRange(from: string, to: string): RangeValidation {
  const fromParsed = tryParseLastThreeDigitsBound(from);
  if (!fromParsed.ok) return fromParsed;
  const toParsed = tryParseLastThreeDigitsBound(to);
  if (!toParsed.ok) return toParsed;
  if (fromParsed.value > toParsed.value) {
    return { ok: false, message: "From Last 3 Digits must be less than or equal to To (000–999)." };
  }
  return { ok: true };
}

/** Extract trailing numeric last-three digits from a student number. */
export function extractLastThreeDigits(studentNumber: string | null | undefined): number | null {
  const digits = (studentNumber ?? "").replace(/\D/g, "");
  if (!digits) return null;
  const last = digits.length <= 3 ? digits : digits.slice(-3);
  const value = Number.parseInt(last, 10);
  if (!Number.isFinite(value) || value < 0 || value > 999) return null;
  return value;
}

export function isStudentNumberInRange(
  studentNumber: string | null | undefined,
  from: string,
  to: string,
): boolean {
  const n = normalizeStudentNumber(studentNumber);
  if (!n) return false;
  return compareStudentNumbers(from, n) <= 0 && compareStudentNumbers(n, to) <= 0;
}

export function isLastThreeDigitsInRange(
  studentNumber: string | null | undefined,
  from: string,
  to: string,
): boolean {
  const fromParsed = tryParseLastThreeDigitsBound(from);
  const toParsed = tryParseLastThreeDigitsBound(to);
  if (!fromParsed.ok || !toParsed.ok || fromParsed.value > toParsed.value) return false;
  const last3 = extractLastThreeDigits(studentNumber);
  if (last3 === null) return false;
  return last3 >= fromParsed.value && last3 <= toParsed.value;
}

export function distinctFacetValues(
  students: readonly AllocationContextStudent[],
  mode: FacetMode,
): string[] {
  const accessor = FACET_ACCESSORS[mode];
  const set = new Set<string>();
  for (const s of students) {
    const v = accessor(s)?.trim();
    if (v) set.add(v);
  }
  return [...set].sort((a, b) => compareStudentNumbers(a, b));
}

function studentMatchesFilter(
  s: AllocationContextStudent,
  filter: PopulationFilterState,
): boolean {
  if (filter.mode === "All") return true;
  if (filter.mode === "StudentNumberRange") {
    const range = validateStudentNumberRange(filter.fromStudentNumber, filter.toStudentNumber);
    if (!range.ok) return false;
    return isStudentNumberInRange(
      s.studentNumber,
      filter.fromStudentNumber.trim(),
      filter.toStudentNumber.trim(),
    );
  }
  if (filter.mode === "LastThreeDigitsRange") {
    return isLastThreeDigitsInRange(s.studentNumber, filter.fromStudentNumber, filter.toStudentNumber);
  }
  const facet = filter.facetValue.trim();
  if (!facet) return false;
  const accessor = FACET_ACCESSORS[filter.mode];
  return compareStudentNumbers(accessor(s), facet) === 0;
}

/**
 * Count matches without allocating a full filtered array (thousands-safe for chips/summaries).
 */
export function countPopulationFilter(
  students: readonly AllocationContextStudent[],
  filter: PopulationFilterState,
): number {
  if (filter.mode === "All") return students.length;
  if (filter.mode === "StudentNumberRange") {
    const range = validateStudentNumberRange(filter.fromStudentNumber, filter.toStudentNumber);
    if (!range.ok) return 0;
  } else if (filter.mode === "LastThreeDigitsRange") {
    const range = validateLastThreeDigitsRange(filter.fromStudentNumber, filter.toStudentNumber);
    if (!range.ok) return 0;
  } else if (!filter.facetValue.trim()) {
    return 0;
  }
  let n = 0;
  for (const s of students) {
    if (studentMatchesFilter(s, filter)) n += 1;
  }
  return n;
}

/**
 * Take at most `limit` matching students without materializing the full match set.
 * Prompt 19 — keep allocation UI usable with large context cohorts.
 */
export function takePopulationFilter(
  students: readonly AllocationContextStudent[],
  filter: PopulationFilterState,
  limit: number,
): AllocationContextStudent[] {
  if (limit <= 0) return [];
  if (filter.mode === "All") {
    return students.length <= limit ? (students as AllocationContextStudent[]) : students.slice(0, limit);
  }
  if (filter.mode === "StudentNumberRange") {
    const range = validateStudentNumberRange(filter.fromStudentNumber, filter.toStudentNumber);
    if (!range.ok) return [];
  } else if (filter.mode === "LastThreeDigitsRange") {
    const range = validateLastThreeDigitsRange(filter.fromStudentNumber, filter.toStudentNumber);
    if (!range.ok) return [];
  } else if (!filter.facetValue.trim()) {
    return [];
  }
  const out: AllocationContextStudent[] = [];
  for (const s of students) {
    if (!studentMatchesFilter(s, filter)) continue;
    out.push(s);
    if (out.length >= limit) break;
  }
  return out;
}

export function applyPopulationFilter(
  students: readonly AllocationContextStudent[],
  filter: PopulationFilterState,
): AllocationContextStudent[] {
  // Never mutate the source context array.
  if (filter.mode === "All") {
    // Avoid cloning thousands of rows when callers only need a read-only view.
    return students as AllocationContextStudent[];
  }

  if (filter.mode === "StudentNumberRange") {
    const range = validateStudentNumberRange(filter.fromStudentNumber, filter.toStudentNumber);
    if (!range.ok) return [];
    const from = filter.fromStudentNumber.trim();
    const to = filter.toStudentNumber.trim();
    return students.filter((s) => isStudentNumberInRange(s.studentNumber, from, to));
  }

  if (filter.mode === "LastThreeDigitsRange") {
    const range = validateLastThreeDigitsRange(filter.fromStudentNumber, filter.toStudentNumber);
    if (!range.ok) return [];
    return students.filter((s) =>
      isLastThreeDigitsInRange(s.studentNumber, filter.fromStudentNumber, filter.toStudentNumber),
    );
  }

  const facet = filter.facetValue.trim();
  if (!facet) return [];
  const accessor = FACET_ACCESSORS[filter.mode];
  return students.filter((s) => compareStudentNumbers(accessor(s), facet) === 0);
}

/** Count unassigned among matches without building the full filtered array. */
export function countUnassignedMatches(
  students: readonly AllocationContextStudent[],
  filter: PopulationFilterState,
): number {
  let n = 0;
  for (const s of students) {
    if (!studentMatchesFilter(s, filter)) continue;
    if (!s.currentSectionId) n += 1;
  }
  return n;
}

export function populationFilterLabel(mode: PopulationFilterMode): string {
  switch (mode) {
    case "All":
      return "All eligible students";
    case "StudentNumberRange":
      return "Full Student Number";
    case "LastThreeDigitsRange":
      return "Last 3 Digits";
    case "Gender":
      return "Gender";
    case "ScholarshipCategory":
      return "Scholarship Category";
    case "MinorSubject":
      return "Minor Subject";
    case "Language":
      return "Language";
    case "TransportRoute":
      return "Transport Route";
    case "Hostel":
      return "Hostel";
    case "ElectiveCombination":
      return "Elective Combination";
    case "Merit":
      return "Merit";
    default:
      return mode;
  }
}

export type FacetReadiness = "Available" | "Unavailable" | "PartiallyAvailable";

/** Facet readiness from Allocation Context only — never invent values. */
export function facetReadiness(
  students: readonly AllocationContextStudent[],
  mode: FacetMode,
): FacetReadiness {
  if (students.length === 0) return "Unavailable";
  const accessor = FACET_ACCESSORS[mode];
  let withValue = 0;
  for (const s of students) {
    if (accessor(s)?.trim()) withValue += 1;
  }
  if (withValue === 0) return "Unavailable";
  if (withValue < students.length) return "PartiallyAvailable";
  return "Available";
}

export function isPopulationModeEnabled(
  students: readonly AllocationContextStudent[],
  mode: PopulationFilterMode,
): boolean {
  if (mode === "All" || mode === "StudentNumberRange" || mode === "LastThreeDigitsRange") return true;
  return facetReadiness(students, mode) !== "Unavailable";
}

export function populationSummaryLabel(filter: PopulationFilterState, matchingCount: number): string {
  if (filter.mode === "All") return `All eligible students · Matching students: ${matchingCount}`;
  if (filter.mode === "StudentNumberRange") {
    return `Full student number ${filter.fromStudentNumber || "?"}–${filter.toStudentNumber || "?"} · Matching students: ${matchingCount}`;
  }
  if (filter.mode === "LastThreeDigitsRange") {
    const from = tryParseLastThreeDigitsBound(filter.fromStudentNumber);
    const to = tryParseLastThreeDigitsBound(filter.toStudentNumber);
    const fromLabel = from.ok ? from.normalized : filter.fromStudentNumber || "?";
    const toLabel = to.ok ? to.normalized : filter.toStudentNumber || "?";
    return `Last 3 Digits ${fromLabel}–${toLabel} · Matching students: ${matchingCount}`;
  }
  return `${populationFilterLabel(filter.mode)}${filter.facetValue ? ` = ${filter.facetValue}` : ""} · Matching students: ${matchingCount}`;
}

/** Map UI filter state to AI29.1D AllocationPopulationSelection API contract. */
export function toAllocationPopulationSelection(filter: PopulationFilterState): {
  mode: string;
  fromStudentNumber?: string;
  toStudentNumber?: string;
  facetValue?: string;
} {
  if (filter.mode === "All") {
    return { mode: "AllEligible" };
  }
  if (filter.mode === "StudentNumberRange") {
    return {
      mode: "StudentNumberRange",
      fromStudentNumber: filter.fromStudentNumber.trim(),
      toStudentNumber: filter.toStudentNumber.trim(),
    };
  }
  if (filter.mode === "LastThreeDigitsRange") {
    const from = tryParseLastThreeDigitsBound(filter.fromStudentNumber);
    const to = tryParseLastThreeDigitsBound(filter.toStudentNumber);
    return {
      mode: "LastThreeDigitsRange",
      fromStudentNumber: from.ok ? from.normalized : filter.fromStudentNumber.trim(),
      toStudentNumber: to.ok ? to.normalized : filter.toStudentNumber.trim(),
    };
  }
  return {
    mode: filter.mode,
    facetValue: filter.facetValue.trim(),
  };
}
