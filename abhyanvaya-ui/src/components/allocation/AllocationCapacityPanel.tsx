import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert,
  Button,
  Checkbox,
  Chip,
  CircularProgress,
  FormControl,
  FormControlLabel,
  FormGroup,
  FormHelperText,
  Radio,
  RadioGroup,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import {
  getCapacityPolicy,
  getSectionOccupancy,
  type SectionCapacitySnapshotDto,
  type TenantSectionCapacityPolicyDto,
} from "../../services/sectionService";
import type { SectionAllocationContext } from "../../services/allocationPlatformService";
import CapacityViolationBanner from "./CapacityViolationBanner";
import type { EngineConstraintEval } from "../../utils/allocationCapacityViolations";
import {
  MSG_ALL_ELIGIBLE_HELPER,
  MSG_EXPLICIT_HELPER,
  MSG_NO_ELIGIBLE_SECTIONS,
  MSG_SELECT_AT_LEAST_ONE_SECTION,
  MSG_TARGET_SECTIONS_HELPER,
  MSG_UNABLE_TO_LOAD_ELIGIBLE_SECTIONS,
  canContinueWithTargetSections,
  filterOccupancyToContextSections,
  formatSelectedTargetSectionsLabel,
  selectedTargetSectionCount,
  targetSectionMode,
  toggleExplicitSectionId,
} from "../../utils/allocationTargetSectionSelection";

type Props = {
  academicYearId: number;
  semesterId: number;
  /** Target sections from Allocation Context — used to scope capacity engine rows. */
  context: SectionAllocationContext | null;
  /** Engine constraint evaluations from simulate/run (if already available). */
  constraints?: readonly EngineConstraintEval[] | null;
  proposedSummaries?: readonly {
    sectionId: number;
    sectionCode: string;
    assignedCount: number;
    maximumCapacity: number;
    occupancyPercent?: number;
  }[] | null;
  /** null = all eligible sections; otherwise explicit target section ids for the engine. */
  targetSectionIds?: number[] | null;
  onTargetSectionIdsChange?: (next: number[] | null) => void;
  /** When true, eligible sections failed to load — fail closed (no catalog fallback). */
  eligibleSectionsError?: boolean;
  onRetryEligibleSections?: () => void;
};

function statusColor(status: string): "default" | "success" | "warning" | "error" | "info" {
  const s = status.toLowerCase();
  if (s.includes("over")) return "error";
  if (s.includes("warning")) return "warning";
  if (s.includes("under")) return "info";
  if (s === "ok" || s === "healthy") return "success";
  return "default";
}

/**
 * AI29.1D — Section Capacity Engine integration for allocation workspace.
 * Displays engine-authored occupancy/policy only; does not compute capacity.
 * AI29.1D.24B.2 — Target Sections scoped to Allocation Context only (fail-closed).
 */
const AllocationCapacityPanel = ({
  academicYearId,
  semesterId,
  context,
  constraints,
  proposedSummaries,
  targetSectionIds = null,
  onTargetSectionIdsChange,
  eligibleSectionsError = false,
  onRetryEligibleSections,
}: Props) => {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [source, setSource] = useState<"capacity-engine" | "allocation-context" | "none">("none");
  const [policy, setPolicy] = useState<TenantSectionCapacityPolicyDto | null>(null);
  const [rows, setRows] = useState<SectionCapacitySnapshotDto[]>([]);

  const contextSectionIds = useMemo(
    () => (context?.sections ?? []).map((s) => s.sectionId),
    [context?.sections],
  );
  const targetIds = useMemo(() => new Set(contextSectionIds), [contextSectionIds]);

  const loadFromContextFallback = useCallback(() => {
    if (targetIds.size === 0) {
      setRows([]);
      setSource("none");
      return;
    }
    const mapped: SectionCapacitySnapshotDto[] = (context?.capacities ?? [])
      .filter((c) => targetIds.has(c.sectionId))
      .map((c) => {
        const sec = context?.sections?.find((s) => s.sectionId === c.sectionId);
        return {
          sectionId: c.sectionId,
          sectionCode: sec?.sectionCode ?? String(c.sectionId),
          sectionName: sec?.sectionName ?? "",
          lifecycleStatus: sec?.lifecycle ?? "",
          maximumCapacity: c.maximumCapacity,
          minimumCapacity: c.minimumCapacity ?? 0,
          recommendedCapacity: c.recommendedCapacity ?? c.maximumCapacity,
          currentStrength: c.currentStrength,
          reservedSeats: c.reservedSeats ?? 0,
          waitingList: c.waitingList ?? 0,
          availableSeats: c.availableCapacity,
          occupancyPercent: c.occupancyPercent,
          capacityStatus: c.capacityStatus,
          isOverCapacity: /over/i.test(c.capacityStatus),
          isUnderCapacity: /under/i.test(c.capacityStatus),
          isHardLimitBreached: /over/i.test(c.capacityStatus),
          hasWarning: /warning|over|under/i.test(c.capacityStatus),
          warnings: [],
        };
      });
    setRows(mapped);
    setSource("allocation-context");
  }, [context, targetIds]);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    // Fail-closed: never display a year/semester-wide catalog when context has no section ids.
    if (targetIds.size === 0) {
      setRows([]);
      setSource("none");
      setLoading(false);
      return;
    }
    const sectionIds = [...targetIds];
    try {
      const [occRes, polRes] = await Promise.all([
        getSectionOccupancy({ academicYearId, semesterId, sectionIds }),
        getCapacityPolicy(),
      ]);
      setPolicy(polRes.data);
      const scoped = filterOccupancyToContextSections(occRes.data ?? [], targetIds);
      if (scoped.length === 0 && (context?.capacities?.length ?? 0) > 0) {
        loadFromContextFallback();
        setError("No capacity rows for the selected sections — showing capacity from the current academic scope.");
      } else {
        setRows(scoped);
        setSource("capacity-engine");
      }
    } catch {
      // Fail-closed: do not retain a previous foreign catalog; only scoped context capacities.
      setRows([]);
      loadFromContextFallback();
      setError("Could not refresh live capacity — showing capacity from the current academic scope.");
    } finally {
      setLoading(false);
    }
  }, [academicYearId, semesterId, targetIds, context?.capacities?.length, loadFromContextFallback]);

  useEffect(() => {
    void load();
  }, [load]);

  // Clear capacity rows immediately when authoritative section set disappears (scope change / load failure).
  useEffect(() => {
    if (eligibleSectionsError || targetIds.size === 0) {
      setRows([]);
      setSource("none");
    }
  }, [eligibleSectionsError, targetIds.size]);

  const contextSections = context?.sections ?? [];
  const mode = targetSectionMode(targetSectionIds);
  const selectedCount = selectedTargetSectionCount(targetSectionIds);
  const canContinueTargets = canContinueWithTargetSections(
    targetSectionIds,
    contextSections.length,
  );

  const toggleSection = (sectionId: number, checked: boolean) => {
    if (!onTargetSectionIdsChange) return;
    onTargetSectionIdsChange(toggleExplicitSectionId(targetSectionIds ?? null, sectionId, checked));
  };

  return (
    <Stack spacing={1.5}>
      <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap", alignItems: "center" }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
          Section Capacity
        </Typography>
        <Button size="small" variant="outlined" onClick={() => void load()} disabled={loading || targetIds.size === 0}>
          Refresh Capacity
        </Button>
        {loading && <CircularProgress size={18} />}
        {source !== "none" && (
          <Chip
            size="small"
            variant="outlined"
            label={source === "capacity-engine" ? "Source: Live capacity" : "Source: Scope capacity"}
          />
        )}
      </Stack>

      <Alert severity="info">
        Occupancy and status come from the server capacity service for this academic scope. The page does not calculate
        capacity itself.
      </Alert>

      <Stack spacing={1}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
          Target Sections
        </Typography>
        <Typography variant="body2" color="text.secondary">
          {MSG_TARGET_SECTIONS_HELPER}
        </Typography>

        {eligibleSectionsError ? (
          <Alert
            severity="error"
            action={
              onRetryEligibleSections ? (
                <Button color="inherit" size="small" onClick={onRetryEligibleSections}>
                  Retry
                </Button>
              ) : undefined
            }
          >
            {MSG_UNABLE_TO_LOAD_ELIGIBLE_SECTIONS}
          </Alert>
        ) : (
          <FormControl component="fieldset" disabled={!onTargetSectionIdsChange || contextSections.length === 0}>
            <RadioGroup
              value={mode}
              onChange={(_, v) => {
                if (v === "all") onTargetSectionIdsChange?.(null);
                else onTargetSectionIdsChange?.([]);
              }}
            >
              <FormControlLabel
                value="all"
                control={<Radio size="small" />}
                label="All eligible sections"
              />
              <FormHelperText sx={{ mt: 0, ml: 4 }}>{MSG_ALL_ELIGIBLE_HELPER}</FormHelperText>
              <FormControlLabel value="explicit" control={<Radio size="small" />} label="Explicit selection" />
              <FormHelperText sx={{ mt: 0, ml: 4 }}>{MSG_EXPLICIT_HELPER}</FormHelperText>
            </RadioGroup>
          </FormControl>
        )}

        {!eligibleSectionsError && mode === "explicit" && contextSections.length > 0 && (
          <>
            <FormGroup sx={{ gap: 0.5 }}>
              {contextSections.map((s) => {
                const checked = (targetSectionIds ?? []).includes(s.sectionId);
                return (
                  <FormControlLabel
                    key={s.sectionId}
                    control={
                      <Checkbox
                        size="small"
                        checked={checked}
                        onChange={(_, c) => toggleSection(s.sectionId, c)}
                        disabled={!onTargetSectionIdsChange}
                        slotProps={{
                          input: { "aria-label": `Select section ${s.sectionCode}` },
                        }}
                      />
                    }
                    label={`${s.sectionCode}${s.sectionName ? ` — ${s.sectionName}` : ""}`}
                  />
                );
              })}
            </FormGroup>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {formatSelectedTargetSectionsLabel(selectedCount)}
            </Typography>
            {!canContinueTargets && (
              <Alert severity="warning">{MSG_SELECT_AT_LEAST_ONE_SECTION}</Alert>
            )}
          </>
        )}

        {!eligibleSectionsError && mode === "all" && contextSections.length > 0 && (
          <Alert severity="success">
            All eligible sections ({contextSections.length}) for the selected academic scope.
          </Alert>
        )}

        {!eligibleSectionsError && !contextSections.length && (
          <Alert severity="warning">{MSG_NO_ELIGIBLE_SECTIONS}</Alert>
        )}
      </Stack>

      {policy && (
        <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
          <Chip
            size="small"
            color={policy.enforceHardLimit ? "error" : "default"}
            label={`Hard limit: ${policy.enforceHardLimit ? "Enforced" : "Off"}`}
          />
          <Chip
            size="small"
            color={policy.softLimitEnabled ? "warning" : "default"}
            label={`Soft limit: ${policy.softLimitEnabled ? "Enabled" : "Off"}`}
          />
          <Chip
            size="small"
            label={`Warning threshold: ${policy.warningPercent}%${policy.autoWarningEnabled ? "" : " (auto off)"}`}
          />
          <Chip size="small" label={`Under-capacity policy: ≤ ${policy.underCapacityPercent}%`} />
        </Stack>
      )}

      {!policy && source === "allocation-context" && (
        <Alert severity="warning">
          Capacity policy could not be loaded. Status values still reflect capacity from the current academic scope.
        </Alert>
      )}

      {error && <Alert severity="warning">{error}</Alert>}

      <CapacityViolationBanner constraints={constraints} proposedSummaries={proposedSummaries} />

      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Section</TableCell>
            <TableCell>Capacity</TableCell>
            <TableCell>Current Occupancy</TableCell>
            <TableCell>Available Capacity</TableCell>
            <TableCell>Occupancy %</TableCell>
            <TableCell>Capacity Status</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {rows.map((r) => (
            <TableRow key={r.sectionId} selected={r.isOverCapacity || r.isHardLimitBreached}>
              <TableCell>
                {r.sectionCode}
                {r.sectionName ? ` — ${r.sectionName}` : ""}
              </TableCell>
              <TableCell>
                {r.maximumCapacity}
                {(r.minimumCapacity > 0 || r.recommendedCapacity > 0) && (
                  <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                    Min {r.minimumCapacity} · Rec {r.recommendedCapacity}
                    {r.reservedSeats > 0 ? ` · Reserved ${r.reservedSeats}` : ""}
                  </Typography>
                )}
              </TableCell>
              <TableCell>{r.currentStrength}</TableCell>
              <TableCell>{r.availableSeats}</TableCell>
              <TableCell>{r.occupancyPercent}%</TableCell>
              <TableCell>
                <Chip size="small" color={statusColor(r.capacityStatus)} label={r.capacityStatus} />
                {r.isHardLimitBreached && (
                  <Typography variant="caption" color="error" sx={{ display: "block" }}>
                    Hard limit breached
                  </Typography>
                )}
                {r.isUnderCapacity && !r.isOverCapacity && (
                  <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                    Under-capacity
                  </Typography>
                )}
              </TableCell>
            </TableRow>
          ))}
          {!rows.length && !loading && (
            <TableRow>
              <TableCell colSpan={6}>
                <Typography variant="body2" color="text.secondary">
                  No target section capacity rows for this academic scope.
                </Typography>
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>

      {rows.some((r) => r.warnings?.length) && (
        <Stack spacing={0.5}>
          {rows.flatMap((r) =>
            (r.warnings ?? []).map((w, i) => (
              <Alert key={`${r.sectionId}-w-${i}`} severity={r.isOverCapacity ? "error" : "warning"}>
                {r.sectionCode}: {w}
              </Alert>
            )),
          )}
        </Stack>
      )}
    </Stack>
  );
};

export default AllocationCapacityPanel;
