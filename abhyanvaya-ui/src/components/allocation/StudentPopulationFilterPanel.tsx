import {
  Alert,
  Box,
  Button,
  Chip,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import {
  countPopulationFilter,
  DEFAULT_POPULATION_FILTER,
  distinctFacetValues,
  facetReadiness,
  isPopulationModeEnabled,
  POPULATION_FILTER_MODES,
  populationFilterLabel,
  populationSummaryLabel,
  validateLastThreeDigitsRange,
  validateStudentNumberRange,
  type AllocationContextStudent,
  type PopulationFilterMode,
  type PopulationFilterState,
} from "../../utils/allocationPopulationFilter";

type Props = {
  students: readonly AllocationContextStudent[];
  filter: PopulationFilterState;
  onChange: (next: PopulationFilterState) => void;
};

/**
 * AI29.1D — Population filters over Allocation Context students (read-only).
 * AI29.1D.24B.4 — Full Student Number vs Last 3 Digits range semantics.
 */
const StudentPopulationFilterPanel = ({ students, filter, onChange }: Props) => {
  const isFullNumberRange = filter.mode === "StudentNumberRange";
  const isLast3Range = filter.mode === "LastThreeDigitsRange";
  const isRangeMode = isFullNumberRange || isLast3Range;

  const rangeValidation = isFullNumberRange
    ? validateStudentNumberRange(filter.fromStudentNumber, filter.toStudentNumber)
    : isLast3Range
      ? validateLastThreeDigitsRange(filter.fromStudentNumber, filter.toStudentNumber)
      : ({ ok: true } as const);

  const needsFacet = filter.mode !== "All" && !isRangeMode;
  type FacetOnly = Exclude<PopulationFilterMode, "All" | "StudentNumberRange" | "LastThreeDigitsRange">;
  const facetMode: FacetOnly | null = needsFacet ? (filter.mode as FacetOnly) : null;
  const facetOptions = facetMode ? distinctFacetValues(students, facetMode) : [];

  const matchedCount = countPopulationFilter(students, filter);
  const readiness = facetMode ? facetReadiness(students, facetMode) : ("Available" as const);
  const modeEnabled = isPopulationModeEnabled(students, filter.mode);
  const facetReady = !needsFacet || Boolean(filter.facetValue.trim());
  const rangeReady = !isRangeMode || rangeValidation.ok;
  const showEmpty =
    students.length > 0 && modeEnabled && facetReady && rangeReady && matchedCount === 0 && filter.mode !== "All";

  return (
    <Stack spacing={1.5}>
      <Alert severity="info">
        Population: {populationSummaryLabel(filter, matchedCount)}. Students are selected from the current academic
        scope. Final placement is decided by the server.
      </Alert>
      <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap", alignItems: "center" }}>
        <TextField
          select
          size="small"
          label="Population filter"
          value={filter.mode}
          onChange={(e) =>
            onChange({
              ...filter,
              mode: e.target.value as PopulationFilterMode,
              facetValue: "",
            })
          }
          sx={{ minWidth: 260 }}
        >
          {POPULATION_FILTER_MODES.map((m) => {
            const enabled = isPopulationModeEnabled(students, m);
            const ready =
              m === "All" || m === "StudentNumberRange" || m === "LastThreeDigitsRange"
                ? "Available"
                : facetReadiness(students, m as Exclude<PopulationFilterMode, "All" | "StudentNumberRange" | "LastThreeDigitsRange">);
            return (
              <MenuItem key={m} value={m} disabled={!enabled}>
                {populationFilterLabel(m)}
                {m !== "All" && m !== "StudentNumberRange" && m !== "LastThreeDigitsRange" ? ` (${ready})` : ""}
              </MenuItem>
            );
          })}
        </TextField>
        <Button size="small" variant="outlined" onClick={() => onChange({ ...DEFAULT_POPULATION_FILTER })}>
          Reset Filters
        </Button>
        <Chip size="small" color="primary" label={`Matching students: ${matchedCount}`} />
        <Chip size="small" variant="outlined" label={`${students.length} in context`} />
        {needsFacet && (
          <Chip
            size="small"
            color={readiness === "Unavailable" ? "default" : readiness === "PartiallyAvailable" ? "warning" : "success"}
            label={`Facet: ${readiness}`}
          />
        )}
      </Stack>
      {!modeEnabled && (
        <Alert severity="warning">
          {populationFilterLabel(filter.mode)} is Unavailable — Allocation Context has no authoritative facet values.
          The criterion is disabled and will not be sent to the engine. Choose All eligible or another Available
          criterion.
        </Alert>
      )}

      {isFullNumberRange && (
        <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5}>
          <TextField
            size="small"
            label="From full Student Number"
            value={filter.fromStudentNumber}
            onChange={(e) => onChange({ ...filter, fromStudentNumber: e.target.value })}
            sx={{ minWidth: 200 }}
            helperText="Compares the complete student number (ordinal, not last 3 digits)."
          />
          <TextField
            size="small"
            label="To full Student Number"
            value={filter.toStudentNumber}
            onChange={(e) => onChange({ ...filter, toStudentNumber: e.target.value })}
            sx={{ minWidth: 200 }}
            error={Boolean(filter.fromStudentNumber && filter.toStudentNumber && !rangeValidation.ok)}
            helperText={
              filter.fromStudentNumber && filter.toStudentNumber && !rangeValidation.ok
                ? rangeValidation.message
                : "From must be ≤ To under full student-number semantics."
            }
          />
        </Stack>
      )}

      {isLast3Range && (
        <Stack spacing={1}>
          <Typography variant="caption" color="text.secondary">
            Filter by the last three digits of the student number (000–999). Example: 046–050 matches …046 through
            …050. Values like 1–5 are normalized to 001–005. This does not change the allocation strategy.
          </Typography>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5}>
            <TextField
              size="small"
              label="From Last 3 Digits"
              value={filter.fromStudentNumber}
              onChange={(e) => onChange({ ...filter, fromStudentNumber: e.target.value })}
              sx={{ minWidth: 200 }}
              placeholder="046"
              helperText="000–999 (normalized to three digits)."
            />
            <TextField
              size="small"
              label="To Last 3 Digits"
              value={filter.toStudentNumber}
              onChange={(e) => onChange({ ...filter, toStudentNumber: e.target.value })}
              sx={{ minWidth: 200 }}
              placeholder="050"
              error={Boolean(filter.fromStudentNumber && filter.toStudentNumber && !rangeValidation.ok)}
              helperText={
                filter.fromStudentNumber && filter.toStudentNumber && !rangeValidation.ok
                  ? rangeValidation.message
                  : "From must be ≤ To as numeric last-three digits."
              }
            />
          </Stack>
        </Stack>
      )}

      {needsFacet && (
        <Box>
          {facetOptions.length === 0 ? (
            <Alert severity="info">
              No {populationFilterLabel(filter.mode)} values are present on students in this Allocation
              Context. Filtering stays on the context contract — values appear when the context supplies
              them.
            </Alert>
          ) : (
            <TextField
              select
              size="small"
              label={populationFilterLabel(filter.mode)}
              value={filter.facetValue}
              onChange={(e) => onChange({ ...filter, facetValue: e.target.value })}
              sx={{ minWidth: 240 }}
            >
              <MenuItem value="">
                <em>Select value</em>
              </MenuItem>
              {facetOptions.map((v) => (
                <MenuItem key={v} value={v}>
                  {v}
                </MenuItem>
              ))}
            </TextField>
          )}
        </Box>
      )}

      {showEmpty && (
        <Alert severity="warning">
          No students match the current population filter. Adjust or reset filters before allocation.
        </Alert>
      )}

      {filter.mode === "All" && (
        <Typography variant="caption" color="text.secondary">
          Showing all eligible students from the Allocation Context for this academic scope.
        </Typography>
      )}
    </Stack>
  );
};

export default StudentPopulationFilterPanel;
