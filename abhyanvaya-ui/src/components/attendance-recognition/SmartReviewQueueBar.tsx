import {
  Box,
  Chip,
  FormControlLabel,
  LinearProgress,
  Stack,
  Switch,
  Typography,
} from "@mui/material";
import {
  SMART_QUEUE_CATEGORIES,
  type SmartQueueCategory,
} from "../../utils/smartReviewQueue";

export type SmartReviewQueueBarProps = {
  counts: Record<SmartQueueCategory, number>;
  activeCategory: SmartQueueCategory | "all";
  onlyPending: boolean;
  pendingCount: number;
  estimatedMinutes: number;
  onCategoryChange: (category: SmartQueueCategory | "all") => void;
  onOnlyPendingChange: (value: boolean) => void;
};

/** AI22.7A Phase 5.4 — smart queue filters + remaining estimate. */
export function SmartReviewQueueBar({
  counts,
  activeCategory,
  onlyPending,
  pendingCount,
  estimatedMinutes,
  onCategoryChange,
  onOnlyPendingChange,
}: SmartReviewQueueBarProps) {
  const total = Object.values(counts).reduce((a, b) => a + b, 0);
  const done = counts.approved;
  const progress = total > 0 ? (done / total) * 100 : 0;

  return (
    <Stack spacing={1} aria-label="Smart review queue">
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center" }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
          Smart Queue
        </Typography>
        <Typography variant="caption" color="text.secondary">
          {pendingCount} remaining · ~{estimatedMinutes} min
        </Typography>
      </Stack>
      <LinearProgress variant="determinate" value={progress} aria-label="Smart queue progress" sx={{ height: 6, borderRadius: 1 }} />
      <Stack direction="row" spacing={0.75} sx={{ flexWrap: "wrap", gap: 0.75 }}>
        <Chip
          size="small"
          label="All"
          clickable
          color={activeCategory === "all" ? "primary" : "default"}
          variant={activeCategory === "all" ? "filled" : "outlined"}
          onClick={() => onCategoryChange("all")}
        />
        {SMART_QUEUE_CATEGORIES.map((category) => (
          <Chip
            key={category.id}
            size="small"
            label={`${category.label} (${counts[category.id]})`}
            clickable
            color={activeCategory === category.id ? "secondary" : "default"}
            variant={activeCategory === category.id ? "filled" : "outlined"}
            onClick={() => onCategoryChange(category.id)}
          />
        ))}
      </Stack>
      <FormControlLabel
        control={
          <Switch
            size="small"
            checked={onlyPending}
            onChange={(event) => onOnlyPendingChange(event.target.checked)}
            slotProps={{ input: { "aria-label": "Show only pending reviews" } }}
          />
        }
        label={<Typography variant="caption">Only pending · auto-collapse approved</Typography>}
      />
      <Box />
    </Stack>
  );
}

export default SmartReviewQueueBar;
