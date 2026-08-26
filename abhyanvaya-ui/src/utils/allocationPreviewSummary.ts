/**
 * AI29.1D — Derive allocation preview tables/summaries from engine AllocationExecutionResult only.
 * No second explanation or scoring engine.
 * AI29.1D.24B.4A.2 — Tolerates missing optional fields and numeric constraint priorities.
 */

import type {
  AllocationExecutionResult,
  AllocationStudentRecommendation,
  AllocationTraceStep,
} from "../services/allocationPlatformService";
import { normalizeAllocationConstraintPriority } from "./allocationConstraintPriority";
import {
  getExecutionConstraints,
  getExecutionRecommendations,
  getExecutionSectionSummaries,
  getExecutionTraceSteps,
} from "./allocationExecutionResultAccessors";

export type PreviewConstraintBucket = {
  capacityViolations: number;
  mandatoryViolations: number;
  preferredViolations: number;
  informationalFindings: number;
};

export type PreviewSectionCount = {
  sectionId: number;
  sectionCode: string;
  assignedCount: number;
  label: string;
};

export type PreviewSummary = {
  totalStudents: number;
  allocated: number;
  unallocated: number;
  sectionCounts: PreviewSectionCount[];
  /** First three sections as Section A / B / C for the summary strip. */
  sectionA?: PreviewSectionCount;
  sectionB?: PreviewSectionCount;
  sectionC?: PreviewSectionCount;
  constraints: PreviewConstraintBucket;
  totalScore?: number;
  scoreSummary?: string;
  strategiesSummary: string;
  constraintResultSummary: string;
};

export type PreviewRow = {
  studentId: number;
  studentNumber: string;
  studentName: string;
  currentSection: string;
  proposedSection: string;
  allocationReason: string;
  strategy: string;
  constraintResult: string;
  score: string;
  allocated: boolean;
};

function strategiesFromTrace(steps: AllocationTraceStep[] | undefined, groupingMode?: string): string {
  const executed = (steps ?? [])
    .filter((s) => s.enabled && s.executed)
    .map((s) => s.strategyCode);
  const unique = [...new Set(executed)];
  const parts = [groupingMode ? `Grouping: ${groupingMode}` : null, unique.length ? unique.join(", ") : null].filter(
    Boolean,
  );
  return parts.join(" · ") || "—";
}

function constraintBucket(
  constraints: { constraintCode: string; priority: string | number; satisfied: boolean; summary: string }[] | undefined,
): PreviewConstraintBucket {
  const rows = constraints ?? [];
  let capacityViolations = 0;
  let mandatoryViolations = 0;
  let preferredViolations = 0;
  let informationalFindings = 0;
  for (const c of rows) {
    const priority = normalizeAllocationConstraintPriority(c.priority);
    if (!c.satisfied) {
      if (c.constraintCode === "Capacity" || c.constraintCode === "ReservedSeats") capacityViolations += 1;
      if (priority === "Mandatory") mandatoryViolations += 1;
      else if (priority === "Preferred") preferredViolations += 1;
      else if (priority === "Informational") informationalFindings += 1;
    }
  }
  return { capacityViolations, mandatoryViolations, preferredViolations, informationalFindings };
}

function constraintResultForRecommendation(
  rec: AllocationStudentRecommendation,
  result: AllocationExecutionResult,
): string {
  const notes = getExecutionTraceSteps(result)
    .filter((s) => s.executed && s.constraintNotes?.length)
    .flatMap((s) => s.constraintNotes ?? []);
  const section = getExecutionSectionSummaries(result).find((s) => s.sectionId === rec.toSectionId);
  const capacityBits: string[] = [];
  if (section) {
    capacityBits.push(
      `${section.sectionCode}: ${section.assignedCount}/${section.maximumCapacity} (${section.occupancyPercent}%)`,
    );
  }
  const unsatisfied = getExecutionConstraints(result)
    .filter((c) => !c.satisfied)
    .map(
      (c) =>
        `${c.constraintCode}[${normalizeAllocationConstraintPriority(c.priority)}]: ${c.summary}`,
    );
  if (unsatisfied.length) return unsatisfied.join(" · ");
  if (notes.length) return notes.slice(0, 3).join(" · ");
  if (capacityBits.length) return `OK — ${capacityBits.join("; ")}`;
  return "Satisfied";
}

export function buildAllocationPreviewSummary(
  result: AllocationExecutionResult | null | undefined,
  options?: {
    totalEligibleStudents?: number;
    groupingMode?: string;
  },
): PreviewSummary | null {
  if (!result) return null;
  try {
    const recommendations = getExecutionRecommendations(result);
    const allocatedIds = new Set(recommendations.map((r) => r.studentId));
    const totalStudents = Math.max(options?.totalEligibleStudents ?? 0, allocatedIds.size);
    const allocated = allocatedIds.size;
    const unallocated = Math.max(0, totalStudents - allocated);

    const sectionCounts = [...getExecutionSectionSummaries(result)]
      .sort((a, b) => a.sectionCode.localeCompare(b.sectionCode, undefined, { sensitivity: "base" }))
      .map((s, i) => ({
        sectionId: s.sectionId,
        sectionCode: s.sectionCode,
        assignedCount: s.assignedCount,
        label: i === 0 ? "Section A" : i === 1 ? "Section B" : i === 2 ? "Section C" : s.sectionCode,
      }));

    const constraints = constraintBucket(getExecutionConstraints(result));
    const score = result.scenario?.score ?? result.score;

    return {
      totalStudents,
      allocated,
      unallocated,
      sectionCounts,
      sectionA: sectionCounts[0],
      sectionB: sectionCounts[1],
      sectionC: sectionCounts[2],
      constraints,
      totalScore: score?.totalScore,
      scoreSummary: score?.summary,
      strategiesSummary: strategiesFromTrace(getExecutionTraceSteps(result), options?.groupingMode),
      constraintResultSummary:
        getExecutionConstraints(result)
          .map(
            (c) =>
              `${c.constraintCode} (${normalizeAllocationConstraintPriority(c.priority)}): ${c.satisfied ? "OK" : c.summary}`,
          )
          .join(" · ") || "—",
    };
  } catch {
    // Malformed optional diagnostics must never blank Preview / Test Allocation.
    return {
      totalStudents: options?.totalEligibleStudents ?? 0,
      allocated: 0,
      unallocated: options?.totalEligibleStudents ?? 0,
      sectionCounts: [],
      constraints: {
        capacityViolations: 0,
        mandatoryViolations: 0,
        preferredViolations: 0,
        informationalFindings: 0,
      },
      strategiesSummary: "—",
      constraintResultSummary: "Allocation details could not be summarized from the server response.",
    };
  }
}

export function buildAllocationPreviewRows(
  result: AllocationExecutionResult | null | undefined,
  options?: {
    groupingMode?: string;
    /** Eligible students not in recommendations → Unallocated rows. */
    eligibleStudents?: {
      studentId: number;
      studentNumber?: string | null;
      studentName?: string | null;
      currentSectionCode?: string | null;
    }[];
    /** Prompt 19 — cap total preview rows (default unlimited for tests; UI passes 150). */
    maxRows?: number;
  },
): PreviewRow[] {
  if (!result) return [];
  try {
    const maxRows = options?.maxRows;
    const strategy = strategiesFromTrace(getExecutionTraceSteps(result), options?.groupingMode);
    const scoreVal = result.scenario?.score?.totalScore ?? result.score?.totalScore;
    const scoreText = scoreVal == null ? "—" : String(scoreVal);

    const recommendations = getExecutionRecommendations(result);
    const rows: PreviewRow[] = [];
    for (const r of recommendations) {
      if (maxRows != null && rows.length >= maxRows) break;
      rows.push({
        studentId: r.studentId,
        studentNumber: r.studentNumber ?? String(r.studentId),
        studentName: r.studentName ?? "—",
        currentSection: r.fromSectionCode ?? "—",
        proposedSection: r.toSectionCode || "—",
        allocationReason: (r.explanations ?? []).join("; ") || "—",
        strategy,
        constraintResult: constraintResultForRecommendation(r, result),
        score: scoreText,
        allocated: true,
      });
    }

    if (maxRows != null && rows.length >= maxRows) return rows;

    const allocatedIds = new Set(rows.map((r) => r.studentId));
    // Also mark ids from remaining recommendations so unallocated fill stays correct when capped.
    for (const r of recommendations) allocatedIds.add(r.studentId);

    for (const s of options?.eligibleStudents ?? []) {
      if (maxRows != null && rows.length >= maxRows) break;
      if (allocatedIds.has(s.studentId)) continue;
      rows.push({
        studentId: s.studentId,
        studentNumber: s.studentNumber ?? String(s.studentId),
        studentName: s.studentName ?? "—",
        currentSection: s.currentSectionCode ?? "—",
        proposedSection: "—",
        allocationReason: "Unallocated by engine",
        strategy,
        constraintResult: "—",
        score: "—",
        allocated: false,
      });
    }

    return rows;
  } catch {
    return [];
  }
}
