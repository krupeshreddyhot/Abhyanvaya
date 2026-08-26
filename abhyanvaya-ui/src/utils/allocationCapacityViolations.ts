/**
 * AI29.1D — Present capacity / reserved-seat violations from Allocation Engine constraint results.
 * Does not compute capacity; only interprets engine-authored evaluations.
 */

import {
  normalizeAllocationConstraintPriority,
  type AllocationConstraintPriorityRaw,
} from "./allocationConstraintPriority";

export type EngineConstraintEval = {
  constraintCode: string;
  /** Live API may return numeric enum (0/1/2) or string name. */
  priority: AllocationConstraintPriorityRaw;
  satisfied: boolean;
  summary: string;
};

export type CapacityViolation = {
  constraintCode: string;
  priority: "Mandatory" | "Preferred" | "Informational" | string;
  summary: string;
  isMandatory: boolean;
  isPreferred: boolean;
};

const CAPACITY_CODES = new Set(["Capacity", "ReservedSeats"]);

export function extractCapacityViolations(
  constraints: readonly EngineConstraintEval[] | null | undefined,
): CapacityViolation[] {
  if (!constraints?.length) return [];
  return constraints
    .filter((c) => CAPACITY_CODES.has(c.constraintCode) && !c.satisfied)
    .map((c) => {
      const priority = normalizeAllocationConstraintPriority(c.priority);
      return {
        constraintCode: c.constraintCode,
        priority,
        summary: c.summary || `${c.constraintCode} violation`,
        isMandatory: priority === "Mandatory",
        isPreferred: priority === "Preferred",
      };
    });
}

export function hasMandatoryCapacityViolation(
  constraints: readonly EngineConstraintEval[] | null | undefined,
): boolean {
  return extractCapacityViolations(constraints).some((v) => v.isMandatory);
}

/** Engine section summary rows that already report assigned > maximum (display only). */
export function proposedOverCapacitySections(
  summaries: readonly {
    sectionId: number;
    sectionCode: string;
    assignedCount: number;
    maximumCapacity: number;
    occupancyPercent?: number;
  }[] | null | undefined,
): { sectionCode: string; assignedCount: number; maximumCapacity: number; occupancyPercent?: number }[] {
  if (!summaries?.length) return [];
  return summaries
    .filter((s) => s.maximumCapacity > 0 && s.assignedCount > s.maximumCapacity)
    .map((s) => ({
      sectionCode: s.sectionCode,
      assignedCount: s.assignedCount,
      maximumCapacity: s.maximumCapacity,
      occupancyPercent: s.occupancyPercent,
    }));
}
