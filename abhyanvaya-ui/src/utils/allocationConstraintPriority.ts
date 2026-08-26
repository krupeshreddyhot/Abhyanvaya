/**
 * AI29.1D.24B.4A.2 — Normalize AllocationConstraintPriority from engine JSON.
 *
 * ASP.NET Core serializes C# enums as numbers by default (Mandatory=0, Preferred=1, Informational=2)
 * unless JsonStringEnumConverter is applied. Live `/allocation/simulate` returns numeric priority.
 * UI helpers must accept string | number without throwing (e.g. number.trim is not a function).
 */

export type AllocationConstraintPriorityRaw = string | number | null | undefined;

export type NormalizedConstraintPriority = "Mandatory" | "Preferred" | "Informational" | string;

/**
 * Safe coerce for display / bucketing. Never throws on malformed optional diagnostics.
 */
export function normalizeAllocationConstraintPriority(
  raw: AllocationConstraintPriorityRaw,
): NormalizedConstraintPriority {
  if (raw == null) return "Preferred";
  if (typeof raw === "number") {
    if (raw === 0) return "Mandatory";
    if (raw === 1) return "Preferred";
    if (raw === 2) return "Informational";
    return String(raw);
  }
  const p = String(raw).trim();
  if (!p) return "Preferred";
  if (/^mandatory$/i.test(p) || p === "0") return "Mandatory";
  if (/^preferred$/i.test(p) || p === "1") return "Preferred";
  if (/^informational$/i.test(p) || p === "2") return "Informational";
  return p;
}
