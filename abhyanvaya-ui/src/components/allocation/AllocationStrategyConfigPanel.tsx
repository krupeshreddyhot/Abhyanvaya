import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Alert,
  Box,
  Button,
  Checkbox,
  Chip,
  FormControl,
  FormControlLabel,
  FormGroup,
  FormLabel,
  MenuItem,
  Radio,
  RadioGroup,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import {
  buildSelectedAllocationRulesSummary,
  COMBINED_STRATEGY_PRESET,
  CONSTRAINT_OPTIONS,
  CONSTRAINT_PRIORITIES,
  filterGroupingOptionsByServer,
  PIPELINE_STRATEGY_OPTIONS,
  pipelineLabel,
  type ConstraintPriority,
} from "../../utils/allocationStrategyCatalog";
import { priorityDisplayLabel, priorityHelpText } from "../../utils/allocationAdministratorCopy";

type Props = {
  groupingModes: string[];
  groupingMode: string;
  onGroupingModeChange: (mode: string) => void;
  strategies: Record<string, boolean>;
  onStrategiesChange: (next: Record<string, boolean>) => void;
  rollNumberBandSize: number | null;
  onRollNumberBandSizeChange: (next: number | null) => void;
  existingAssignmentPolicy: "PreserveExisting" | "Reallocate";
  onExistingAssignmentPolicyChange: (next: "PreserveExisting" | "Reallocate") => void;
  constraintPriorities: Record<string, ConstraintPriority>;
  onConstraintPrioritiesChange: (next: Record<string, ConstraintPriority>) => void;
  combinedPresetActive: boolean;
  onCombinedPresetChange: (active: boolean) => void;
  showTechnicalDetails?: boolean;
  /** Server capacity projections for soft band-size warning only (not authoritative placement). */
  targetSectionCapacities?: readonly { sectionId: number; maximumCapacity: number }[];
};

type PlacementPolicy = "Capacity" | "RollNumberBands";

/**
 * AI29.1D.24B.4A — Administrator Allocation Rules (order, placement, existing assignments).
 * Does not score, place, or invent eligibility.
 */
const AllocationStrategyConfigPanel = ({
  groupingModes,
  groupingMode,
  onGroupingModeChange,
  strategies,
  onStrategiesChange,
  rollNumberBandSize,
  onRollNumberBandSizeChange,
  existingAssignmentPolicy,
  onExistingAssignmentPolicyChange,
  constraintPriorities,
  onConstraintPrioritiesChange,
  combinedPresetActive,
  onCombinedPresetChange,
  showTechnicalDetails = false,
  targetSectionCapacities = [],
}: Props) => {
  const groupingOptions = filterGroupingOptionsByServer(groupingModes);
  const placementPolicy: PlacementPolicy = strategies.RollNumberBands ? "RollNumberBands" : "Capacity";
  const summary = buildSelectedAllocationRulesSummary({
    groupingMode,
    enabledStrategies: strategies,
    constraintPriorities,
    combinedPresetActive,
  });

  const administratorStrategyCodes = Object.keys(strategies).filter((code) => {
    if (code === "Capacity" || code === "RollNumberBands") return false;
    const opt = PIPELINE_STRATEGY_OPTIONS.find((p) => p.code === code);
    return !opt?.hideFromAdministratorRules;
  });

  const minCapacity =
    targetSectionCapacities.length > 0
      ? Math.min(...targetSectionCapacities.map((c) => c.maximumCapacity).filter((n) => n > 0))
      : null;
  const bandExceedsCapacity =
    placementPolicy === "RollNumberBands" &&
    rollNumberBandSize != null &&
    rollNumberBandSize > 0 &&
    minCapacity != null &&
    rollNumberBandSize > minCapacity;

  const applyCombinedPreset = () => {
    const next = { ...strategies };
    for (const code of COMBINED_STRATEGY_PRESET.enableStrategies) {
      if (code in next) next[code] = true;
    }
    onStrategiesChange(next);
    onCombinedPresetChange(true);
  };

  const onPrimaryChange = (mode: string) => {
    onGroupingModeChange(mode);
    const opt = groupingOptions.find((g) => g.code === mode);
    if (opt?.enableStrategies?.length) {
      const next = { ...strategies };
      for (const code of opt.enableStrategies) {
        if (code in next) next[code] = true;
      }
      onStrategiesChange(next);
    }
  };

  const onPlacementPolicyChange = (policy: PlacementPolicy) => {
    onStrategiesChange({
      ...strategies,
      Capacity: policy === "Capacity",
      RollNumberBands: policy === "RollNumberBands",
    });
    if (policy === "RollNumberBands" && groupingMode === "Alphabetical") {
      onGroupingModeChange("LastThreeDigits");
    }
  };

  return (
    <Stack spacing={2}>
      <Alert severity="info">
        Choose how eligible students should be organized. Final section placement is decided by the server using
        Section capacity and your selected rules.
      </Alert>

      <Stack spacing={1}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
          Student Order
        </Typography>
        <TextField
          select
          size="small"
          label="Student Order"
          value={groupingMode}
          onChange={(e) => onPrimaryChange(e.target.value)}
          sx={{ maxWidth: 360 }}
          helperText="Determines the order in which eligible students are considered."
        >
          {groupingOptions.map((g) => (
            <MenuItem key={g.code} value={g.code}>
              {g.label}
            </MenuItem>
          ))}
          {groupingModes
            .filter((m) => !groupingOptions.some((g) => g.code === m))
            .map((m) => (
              <MenuItem key={m} value={m}>
                {m}
              </MenuItem>
            ))}
        </TextField>
        <Typography variant="body2" color="text.secondary">
          {groupingOptions.find((g) => g.code === groupingMode)?.explanation ??
            "Order students using the selected rule."}
        </Typography>
      </Stack>

      <Stack spacing={1}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
          Section Allocation Method
        </Typography>
        <TextField
          select
          size="small"
          label="Section Allocation Method"
          value={placementPolicy}
          onChange={(e) => onPlacementPolicyChange(e.target.value as PlacementPolicy)}
          sx={{ maxWidth: 360 }}
          helperText="Determines how students are distributed across the selected Sections."
        >
          <MenuItem value="Capacity">Capacity Balance</MenuItem>
          <MenuItem value="RollNumberBands">Roll Number Bands</MenuItem>
        </TextField>
        <Typography variant="body2" color="text.secondary">
          {placementPolicy === "RollNumberBands"
            ? "Students are placed into target Sections by last-three-digit bands. Band sequence follows the academic Section order from the server."
            : "Students are placed by balancing occupancy across target Sections while respecting capacity."}
        </Typography>
        {placementPolicy === "RollNumberBands" && (
          <Stack spacing={1}>
            <TextField
              size="small"
              type="number"
              label="Band Size"
              value={rollNumberBandSize ?? ""}
              onChange={(e) => {
                const raw = e.target.value.trim();
                if (!raw) {
                  onRollNumberBandSizeChange(null);
                  return;
                }
                const n = Number.parseInt(raw, 10);
                onRollNumberBandSizeChange(Number.isFinite(n) && n > 0 ? n : null);
              }}
              sx={{ maxWidth: 240 }}
              helperText="Leave blank to use the first target Section capacity. Band Size is not the same as Section Capacity."
              slotProps={{ htmlInput: { min: 1, max: 999 } }}
            />
            {bandExceedsCapacity && (
              <Alert severity="warning">
                Your allocation band contains more students than the selected Section can hold. Some students may
                remain unallocated.
              </Alert>
            )}
          </Stack>
        )}
      </Stack>

      <FormControl>
        <FormLabel sx={{ fontWeight: 700, color: "text.primary" }}>Existing Section Assignments</FormLabel>
        <Typography variant="caption" color="text.secondary" sx={{ mb: 0.5 }}>
          Choose whether students who already have a Section should remain there or be reconsidered.
        </Typography>
        <RadioGroup
          value={existingAssignmentPolicy}
          onChange={(_, v) => onExistingAssignmentPolicyChange(v as "PreserveExisting" | "Reallocate")}
        >
          <FormControlLabel
            value="PreserveExisting"
            control={<Radio size="small" />}
            label="Preserve existing assignments"
          />
          <FormControlLabel value="Reallocate" control={<Radio size="small" />} label="Reallocate students" />
        </RadioGroup>
      </FormControl>

      <Stack spacing={1}>
        <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap", alignItems: "center" }}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Additional Allocation Rules
          </Typography>
          <Button size="small" variant={combinedPresetActive ? "contained" : "outlined"} onClick={applyCombinedPreset}>
            {COMBINED_STRATEGY_PRESET.label}
          </Button>
          {combinedPresetActive && <Chip size="small" color="primary" label="Combined rules on" />}
        </Stack>
        <Typography variant="caption" color="text.secondary">
          {COMBINED_STRATEGY_PRESET.explanation}
        </Typography>
        <FormGroup row sx={{ gap: 0.5 }}>
          {administratorStrategyCodes.map((code) => (
            <FormControlLabel
              key={code}
              control={
                <Checkbox
                  size="small"
                  checked={Boolean(strategies[code])}
                  onChange={(_, checked) => {
                    onCombinedPresetChange(false);
                    onStrategiesChange({ ...strategies, [code]: checked });
                  }}
                />
              }
              label={pipelineLabel(code)}
            />
          ))}
        </FormGroup>
      </Stack>

      <Box
        sx={{
          p: 1.5,
          borderRadius: 1,
          border: "1px solid",
          borderColor: "divider",
          bgcolor: "action.hover",
        }}
      >
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
          Selected Allocation Rules
        </Typography>
        <Typography variant="body2">
          <strong>Student Order</strong> — {summary.primaryRule}
        </Typography>
        <Typography variant="body2" sx={{ mt: 0.5 }}>
          <strong>Section Allocation Method</strong> —{" "}
          {placementPolicy === "RollNumberBands" ? "Roll Number Bands" : "Capacity Balance"}
        </Typography>
        <Typography variant="body2" sx={{ mt: 0.5 }}>
          <strong>Existing assignments</strong> —{" "}
          {existingAssignmentPolicy === "Reallocate" ? "Reallocate" : "Preserve"}
        </Typography>
        <Typography variant="body2" sx={{ mt: 1 }}>
          <strong>Section capacity</strong> — {summary.sectionCapacityRequired ? "Required" : "Preferred"}
        </Typography>
      </Box>

      <Accordion disableGutters elevation={0} sx={{ border: "1px solid", borderColor: "divider", borderRadius: 1 }}>
        <AccordionSummary expandIcon={<ExpandMoreIcon />} aria-controls="advanced-allocation-options" id="advanced-allocation-options-header">
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Advanced Allocation Options
          </Typography>
        </AccordionSummary>
        <AccordionDetails>
          <Stack spacing={1.5}>
            <Typography variant="body2" color="text.secondary">
              Set how strongly each rule must be satisfied. Values are sent to the server unchanged.
            </Typography>
            {CONSTRAINT_OPTIONS.filter((c) => c.code in constraintPriorities).map((c) => (
              <Stack
                key={c.code}
                direction={{ xs: "column", sm: "row" }}
                spacing={1}
                sx={{ alignItems: { sm: "center" } }}
              >
                <TextField
                  select
                  size="small"
                  label={c.label}
                  value={constraintPriorities[c.code] ?? "Preferred"}
                  onChange={(e) =>
                    onConstraintPrioritiesChange({
                      ...constraintPriorities,
                      [c.code]: e.target.value as ConstraintPriority,
                    })
                  }
                  sx={{ minWidth: 220 }}
                  helperText={priorityHelpText(constraintPriorities[c.code] ?? "Preferred")}
                >
                  {CONSTRAINT_PRIORITIES.map((p) => (
                    <MenuItem key={p} value={p}>
                      {priorityDisplayLabel(p)}
                    </MenuItem>
                  ))}
                </TextField>
                <Typography variant="body2" color="text.secondary">
                  {c.explanation}
                </Typography>
              </Stack>
            ))}
          </Stack>
        </AccordionDetails>
      </Accordion>

      {showTechnicalDetails && (
        <Accordion disableGutters elevation={0} sx={{ border: "1px dashed", borderColor: "divider", borderRadius: 1 }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />} aria-controls="allocation-rules-technical" id="allocation-rules-technical-header">
            <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
              Technical Details
            </Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Typography variant="caption" color="text.secondary" component="pre" sx={{ m: 0, whiteSpace: "pre-wrap" }}>
              {JSON.stringify(
                {
                  groupingMode,
                  enabledStrategies: strategies,
                  rollNumberBandSize,
                  existingAssignmentPolicy,
                  constraintPriorities,
                },
                null,
                2,
              )}
            </Typography>
          </AccordionDetails>
        </Accordion>
      )}
    </Stack>
  );
};

export default AllocationStrategyConfigPanel;
