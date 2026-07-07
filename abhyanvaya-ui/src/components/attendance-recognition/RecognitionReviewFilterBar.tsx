import {
  Box,
  Button,
  Checkbox,
  Chip,
  FormControlLabel,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import {
  RECOGNITION_REVIEW_FILTERS,
  type RecognitionReviewFilter,
} from "../../utils/recognitionReviewFilters";

type RecognitionReviewFilterBarProps = {
  searchText: string;
  onSearchChange: (value: string) => void;
  activeFilters: Set<RecognitionReviewFilter>;
  onToggleFilter: (filter: RecognitionReviewFilter) => void;
  onClearFilters: () => void;
  totalCount: number;
  filteredCount: number;
  pendingCount: number;
  selectedCount: number;
  allPendingSelected: boolean;
  somePendingSelected: boolean;
  selectionDisabled: boolean;
  onToggleSelectAllPending: () => void;
};

export function RecognitionReviewFilterBar({
  searchText,
  onSearchChange,
  activeFilters,
  onToggleFilter,
  onClearFilters,
  totalCount,
  filteredCount,
  pendingCount,
  selectedCount,
  allPendingSelected,
  somePendingSelected,
  selectionDisabled,
  onToggleSelectAllPending,
}: RecognitionReviewFilterBarProps) {
  return (
    <Stack spacing={1.5}>
      <Stack
        direction={{ xs: "column", sm: "row" }}
        spacing={1}
        sx={{ justifyContent: "space-between", alignItems: { sm: "center" } }}
      >
        <Box>
          <Typography variant="h6" id="recognition-list-heading">
            Recognition list
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {filteredCount} of {totalCount} face(s)
            {pendingCount > 0 ? ` · ${pendingCount} pending` : ""}
            {selectedCount > 0 ? ` · ${selectedCount} selected` : ""}
          </Typography>
        </Box>

        <FormControlLabel
          control={
            <Checkbox
              indeterminate={somePendingSelected && !allPendingSelected}
              checked={allPendingSelected}
              disabled={selectionDisabled || pendingCount === 0}
              onChange={onToggleSelectAllPending}
              slotProps={{ input: { "aria-label": "Select all pending recognitions" } }}
            />
          }
          label="Select all pending"
        />
      </Stack>

      <TextField
        size="small"
        label="Search student number or name"
        value={searchText}
        onChange={(event) => onSearchChange(event.target.value)}
        fullWidth
        slotProps={{ htmlInput: { "aria-label": "Search recognitions by student number or name" } }}
      />

      <Stack direction="row" spacing={0.75} sx={{ flexWrap: "wrap", gap: 0.75, alignItems: "center" }}>
        <Typography variant="caption" color="text.secondary" sx={{ mr: 0.5 }}>
          Filter:
        </Typography>
        <Chip
          label="All"
          size="small"
          clickable
          color={activeFilters.size === 0 ? "primary" : "default"}
          variant={activeFilters.size === 0 ? "filled" : "outlined"}
          onClick={onClearFilters}
        />
        {RECOGNITION_REVIEW_FILTERS.map((filter) => (
          <Chip
            key={filter.id}
            label={filter.label}
            size="small"
            clickable
            color={activeFilters.has(filter.id) ? "primary" : "default"}
            variant={activeFilters.has(filter.id) ? "filled" : "outlined"}
            onClick={() => onToggleFilter(filter.id)}
          />
        ))}
        {activeFilters.size > 0 && (
          <Button size="small" onClick={onClearFilters}>
            Clear filters
          </Button>
        )}
      </Stack>

      <Typography variant="caption" color="text.secondary">
        Keyboard: A Approve · R Reject · I Mark unknown (focused face)
      </Typography>
    </Stack>
  );
}
