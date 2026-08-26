/**
 * AI29.1D Prompt 13 — Combined Section operational class helpers.
 * Consumes TimetableSections / multi-select section ids — does not invent a second combined-section model.
 */

import { normalizeSectionIds } from "./attendanceMarkingScope";

export type CombinedSectionClassView = {
  isCombined: boolean;
  /** Single operational label, e.g. "A" or "A + B + C". */
  operationalLabel: string | null;
  sectionIds: number[];
  sectionCodes: string[];
  /** Short UI title for timetable-driven combined class. */
  displayTitle: string | null;
  subtitle: string | null;
};

export function formatOperationalClassLabel(sectionCodes: readonly string[] | null | undefined): string | null {
  const codes = [...new Set((sectionCodes ?? []).map((c) => c?.trim()).filter(Boolean))] as string[];
  if (codes.length === 0) return null;
  return codes.join(" + ");
}

export function buildCombinedSectionClassView(input: {
  sectionIds?: readonly number[] | null;
  sectionCodes?: readonly string[] | null;
  /** Server operational label when provided. */
  operationalClassLabel?: string | null;
  isCombinedClass?: boolean | null;
}): CombinedSectionClassView {
  const sectionIds = normalizeSectionIds(input.sectionIds);
  const codesFromInput = (input.sectionCodes ?? []).map((c) => String(c).trim()).filter(Boolean);
  const operationalLabel =
    (input.operationalClassLabel && input.operationalClassLabel.trim()) ||
    formatOperationalClassLabel(codesFromInput);
  const isCombined =
    input.isCombinedClass === true || sectionIds.length > 1 || (codesFromInput.length > 1);

  if (!operationalLabel) {
    return {
      isCombined: false,
      operationalLabel: null,
      sectionIds,
      sectionCodes: codesFromInput,
      displayTitle: null,
      subtitle: null,
    };
  }

  if (isCombined) {
    return {
      isCombined: true,
      operationalLabel,
      sectionIds,
      sectionCodes: codesFromInput,
      displayTitle: `Combined class · ${operationalLabel}`,
      subtitle: "One attendance session. Student rows retain underlying Section identity for reporting.",
    };
  }

  return {
    isCombined: false,
    operationalLabel,
    sectionIds,
    sectionCodes: codesFromInput,
    displayTitle: `Section ${operationalLabel}`,
    subtitle: "Attendance population is limited to this section.",
  };
}
