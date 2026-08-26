/**
 * AI29.1D — Human-readable catalog over AI29.1C AllocationPipelineConfig contracts.
 * AI29.1D.24B — administrator-facing labels; no scoring or placement logic.
 */

export type ConstraintPriority = "Mandatory" | "Preferred" | "Informational";

export type GroupingStrategyOption = {
  code: string;
  label: string;
  explanation: string;
  /** When selected as primary, optionally enable matching pipeline strategies. */
  enableStrategies?: string[];
};

export type PipelineStrategyOption = {
  code: string;
  label: string;
  explanation: string;
  /** Core gate strategies — toggles allowed but defaults stay on. */
  core?: boolean;
  /** Hide from Additional Allocation Rules (still sent when enabled by defaults). */
  hideFromAdministratorRules?: boolean;
};

export type ConstraintOption = {
  code: string;
  label: string;
  explanation: string;
};

/** Primary ordering / grouping strategies exposed in the workspace. */
export const GROUPING_STRATEGY_OPTIONS: GroupingStrategyOption[] = [
  {
    code: "StudentNumber",
    label: "Student Number",
    explanation: "Order students by their full student number.",
  },
  {
    code: "LastThreeDigits",
    label: "Last 3 Digits",
    explanation: "Order students using the last three digits of their student number.",
  },
  {
    code: "Alphabetical",
    label: "Alphabetical Order",
    explanation: "Order students alphabetically by name.",
  },
  {
    code: "Gender",
    label: "Gender Balance",
    explanation: "Maintain a balanced distribution of students by gender where possible.",
    enableStrategies: ["Gender"],
  },
  {
    code: "Merit",
    label: "Merit",
    explanation: "Distribute students based on merit information.",
    enableStrategies: ["Merit"],
  },
  {
    code: "Scholarship",
    label: "Scholarship Category",
    explanation: "Consider scholarship categories when distributing students.",
    enableStrategies: ["Scholarship"],
  },
  {
    code: "MinorSubject",
    label: "Minor Subject",
    explanation: "Consider minor subject selections when distributing students.",
  },
  {
    code: "Language",
    label: "Language",
    explanation: "Consider language preferences when distributing students.",
    enableStrategies: ["Language"],
  },
  {
    code: "Transport",
    label: "Transport Route",
    explanation: "Consider transport routes when distributing students.",
    enableStrategies: ["Transport"],
  },
  {
    code: "Hostel",
    label: "Hostel",
    explanation: "Consider hostel allocation when distributing students.",
    enableStrategies: ["Hostel"],
  },
  {
    code: "ElectiveCombination",
    label: "Elective Combination",
    explanation: "Consider elective combinations when distributing students.",
    enableStrategies: ["Elective"],
  },
  {
    code: "StudentNumberRange",
    label: "Student Number Range",
    explanation: "Order students within a student-number range selected on the Student Population step.",
  },
];

/** Weighted / Combined is a UI preset over multiple enabled pipeline strategies (engine already supports this). */
export const COMBINED_STRATEGY_PRESET = {
  code: "WeightedCombined",
  label: "Balanced combination",
  explanation:
    "Apply several additional allocation rules together so distribution considers multiple student attributes.",
  enableStrategies: ["Gender", "Language", "Scholarship", "Elective", "Transport", "Hostel", "Merit", "Scoring"],
} as const;

export const PIPELINE_STRATEGY_OPTIONS: PipelineStrategyOption[] = [
  {
    code: "Validation",
    label: "Validation checks",
    explanation: "Confirm academic scope readiness before placement.",
    core: true,
    hideFromAdministratorRules: true,
  },
  {
    code: "Capacity",
    label: "Section capacity balance",
    explanation: "Place students by balancing occupancy across sections while respecting capacity.",
    core: true,
  },
  {
    code: "RollNumberBands",
    label: "Roll Number Bands",
    explanation:
      "Place students into target sections by last-three-digit bands (band width from section capacity or an explicit band size). Ordering comes from the primary allocation rule.",
    core: true,
  },
  {
    code: "Policy",
    label: "Section policy",
    explanation: "Apply section policy checks from the academic context.",
    core: true,
    hideFromAdministratorRules: true,
  },
  {
    code: "Gender",
    label: "Gender Balance",
    explanation: "Consider gender balance when distributing students.",
  },
  {
    code: "Language",
    label: "Language",
    explanation: "Consider language preferences when distributing students.",
  },
  {
    code: "Scholarship",
    label: "Scholarship Category",
    explanation: "Consider scholarship categories when distributing students.",
  },
  {
    code: "Elective",
    label: "Elective Combination",
    explanation: "Consider elective combinations when distributing students.",
  },
  {
    code: "Transport",
    label: "Transport Route",
    explanation: "Consider transport routes when distributing students.",
  },
  {
    code: "Hostel",
    label: "Hostel",
    explanation: "Consider hostel allocation when distributing students.",
  },
  {
    code: "Merit",
    label: "Merit",
    explanation: "Consider merit information when distributing students.",
  },
  {
    code: "Scoring",
    label: "Allocation scoring",
    explanation: "Produce an overall allocation score from the selected rules.",
    core: true,
    hideFromAdministratorRules: true,
  },
];

export const CONSTRAINT_OPTIONS: ConstraintOption[] = [
  {
    code: "Capacity",
    label: "Section capacity",
    explanation: "Sections must not exceed their maximum capacity.",
  },
  {
    code: "ReservedSeats",
    label: "Reserved seats",
    explanation: "Reserved seat headroom must be respected.",
  },
  {
    code: "GenderBalance",
    label: "Gender Balance",
    explanation: "Prefer balanced gender distribution across sections.",
  },
  {
    code: "Language",
    label: "Language",
    explanation: "Prefer grouping by language where possible.",
  },
  {
    code: "Merit",
    label: "Merit",
    explanation: "Prefer balanced merit distribution where possible.",
  },
  {
    code: "Scholarship",
    label: "Scholarship Category",
    explanation: "Prefer scholarship-aware distribution where possible.",
  },
  {
    code: "ElectiveCombination",
    label: "Elective Combination",
    explanation: "Prefer elective-aware distribution where possible.",
  },
  {
    code: "MinorSubject",
    label: "Minor Subject",
    explanation: "Consider minor subject selections for reporting and preference.",
  },
  {
    code: "Hostel",
    label: "Hostel",
    explanation: "Consider hostel information for reporting and preference.",
  },
  {
    code: "Transport",
    label: "Transport Route",
    explanation: "Consider transport routes for reporting and preference.",
  },
];

export const CONSTRAINT_PRIORITIES: ConstraintPriority[] = ["Mandatory", "Preferred", "Informational"];

export const DEFAULT_CONSTRAINT_PRIORITIES: Record<string, ConstraintPriority> = {
  Capacity: "Mandatory",
  ReservedSeats: "Mandatory",
  GenderBalance: "Preferred",
  Language: "Preferred",
  Merit: "Preferred",
  Hostel: "Informational",
  Transport: "Informational",
  ElectiveCombination: "Preferred",
  MinorSubject: "Informational",
  Scholarship: "Preferred",
};

export function groupingLabel(code: string): string {
  return GROUPING_STRATEGY_OPTIONS.find((g) => g.code === code)?.label ?? code;
}

export function groupingExplanation(code: string): string {
  return (
    GROUPING_STRATEGY_OPTIONS.find((g) => g.code === code)?.explanation ??
    "Arrange students using the selected allocation rule."
  );
}

export function pipelineLabel(code: string): string {
  return PIPELINE_STRATEGY_OPTIONS.find((p) => p.code === code)?.label ?? code;
}

export function pipelineExplanation(code: string): string {
  return (
    PIPELINE_STRATEGY_OPTIONS.find((p) => p.code === code)?.explanation ??
    "Additional allocation rule considered when distributing students."
  );
}

export function constraintExplanation(code: string): string {
  return (
    CONSTRAINT_OPTIONS.find((c) => c.code === code)?.explanation ??
    "Allocation rule evaluated for this distribution."
  );
}

export function constraintLabel(code: string): string {
  return CONSTRAINT_OPTIONS.find((c) => c.code === code)?.label ?? code;
}

export type SelectedAllocationRulesSummary = {
  primaryRule: string;
  additionalRules: string[];
  sectionCapacityRequired: boolean;
};

export function buildSelectedAllocationRulesSummary(input: {
  groupingMode: string;
  enabledStrategies: Record<string, boolean>;
  constraintPriorities: Record<string, ConstraintPriority>;
  combinedPresetActive: boolean;
}): SelectedAllocationRulesSummary {
  const additionalRules = Object.entries(input.enabledStrategies)
    .filter(([, on]) => on)
    .map(([code]) => code)
    .filter((code) => {
      if (code === "Capacity" || code === "RollNumberBands") return false;
      const opt = PIPELINE_STRATEGY_OPTIONS.find((p) => p.code === code);
      return !opt?.hideFromAdministratorRules;
    })
    .map((code) => pipelineLabel(code));

  if (input.combinedPresetActive && !additionalRules.includes(COMBINED_STRATEGY_PRESET.label)) {
    additionalRules.unshift(COMBINED_STRATEGY_PRESET.label);
  }

  return {
    primaryRule: groupingLabel(input.groupingMode),
    additionalRules: [...new Set(additionalRules)],
    sectionCapacityRequired: (input.constraintPriorities.Capacity ?? "Mandatory") === "Mandatory",
  };
}

/** @deprecated Prefer buildSelectedAllocationRulesSummary for administrator UI. */
export function buildSelectedCriteriaExplanations(input: {
  groupingMode: string;
  enabledStrategies: Record<string, boolean>;
  constraintPriorities: Record<string, ConstraintPriority>;
  combinedPresetActive: boolean;
}): string[] {
  const summary = buildSelectedAllocationRulesSummary(input);
  const lines: string[] = [`Primary rule: ${summary.primaryRule} — ${groupingExplanation(input.groupingMode)}`];
  for (const rule of summary.additionalRules) {
    lines.push(`Additional rule: ${rule}`);
  }
  for (const [code, priority] of Object.entries(input.constraintPriorities)) {
    const display =
      priority === "Mandatory" ? "Required" : priority === "Preferred" ? "Preferred" : "Informational";
    lines.push(`${constraintLabel(code)}: ${display} — ${constraintExplanation(code)}`);
  }
  return lines;
}

/** Intersect UI catalog with modes returned by GET /allocation/grouping-modes. */
export function filterGroupingOptionsByServer(modes: string[]): GroupingStrategyOption[] {
  if (!modes.length) return GROUPING_STRATEGY_OPTIONS;
  const set = new Set(modes.map((m) => m.toLowerCase()));
  return GROUPING_STRATEGY_OPTIONS.filter((o) => set.has(o.code.toLowerCase()));
}
