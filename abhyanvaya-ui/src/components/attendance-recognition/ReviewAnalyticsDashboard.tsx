import { Box, Card, CardContent, Stack, Typography } from "@mui/material";
import type { ReviewAnalyticsSnapshot } from "../../utils/reviewAnalytics";

export type ReviewAnalyticsDashboardProps = {
  analytics: ReviewAnalyticsSnapshot;
  compact?: boolean;
};

function MetricCard({ label, value }: { label: string; value: string | number }) {
  return (
    <Card variant="outlined" sx={{ minWidth: 120, flex: "1 1 120px" }}>
      <CardContent sx={{ py: 1.25, px: 1.5, "&:last-child": { pb: 1.25 } }}>
        <Typography variant="caption" color="text.secondary">
          {label}
        </Typography>
        <Typography variant="h6" sx={{ fontWeight: 700, lineHeight: 1.2 }}>
          {value}
        </Typography>
      </CardContent>
    </Card>
  );
}

function BarRow({ label, value, max, color }: { label: string; value: number; max: number; color: string }) {
  const width = max > 0 ? Math.max(4, Math.round((value / max) * 100)) : 0;
  return (
    <Stack spacing={0.25}>
      <Stack direction="row" sx={{ justifyContent: "space-between" }}>
        <Typography variant="caption">{label}</Typography>
        <Typography variant="caption" color="text.secondary">
          {value}
        </Typography>
      </Stack>
      <Box sx={{ height: 8, borderRadius: 1, bgcolor: "action.hover", overflow: "hidden" }}>
        <Box sx={{ width: `${width}%`, height: "100%", bgcolor: color, transition: "width 200ms ease" }} />
      </Box>
    </Stack>
  );
}

/** AI22.7A Phase 5.6 — review analytics from existing recognition DTOs. */
export function ReviewAnalyticsDashboard({ analytics, compact = false }: ReviewAnalyticsDashboardProps) {
  const statusMax = Math.max(
    1,
    analytics.approved,
    analytics.rejected,
    analytics.unknown,
    analytics.duplicates,
    analytics.pending,
  );
  const confMax = Math.max(1, ...analytics.confidenceBuckets.map((b) => b.count));

  return (
    <Stack spacing={1.5} aria-label="Review analytics dashboard">
      <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
        Review Analytics
      </Typography>
      <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", gap: 1 }}>
        <MetricCard label="Images" value={analytics.images} />
        <MetricCard label="Faces" value={analytics.faces} />
        <MetricCard label="Students" value={analytics.students} />
        <MetricCard label="Approved" value={analytics.approved} />
        <MetricCard label="Rejected" value={analytics.rejected} />
        <MetricCard label="Unknown" value={analytics.unknown} />
        <MetricCard label="Duplicates" value={analytics.duplicates} />
        <MetricCard
          label="Avg Confidence"
          value={analytics.averageConfidence == null ? "—" : `${Math.round(analytics.averageConfidence * 100)}%`}
        />
        <MetricCard
          label="Lowest Confidence"
          value={analytics.lowestConfidence == null ? "—" : `${Math.round(analytics.lowestConfidence * 100)}%`}
        />
        {!compact ? (
          <>
            <MetricCard label="Recognition Time" value={analytics.recognitionTimeLabel} />
            <MetricCard label="Review Time" value={analytics.reviewTimeLabel} />
            <MetricCard label="Est. Remaining" value={analytics.estimatedRemainingLabel} />
          </>
        ) : null}
      </Stack>
      <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
        <Stack spacing={0.75} sx={{ flex: 1 }}>
          <Typography variant="caption" sx={{ fontWeight: 700 }}>
            Status distribution
          </Typography>
          <BarRow label="Approved" value={analytics.approved} max={statusMax} color="success.main" />
          <BarRow label="Pending" value={analytics.pending} max={statusMax} color="warning.main" />
          <BarRow label="Rejected" value={analytics.rejected} max={statusMax} color="error.main" />
          <BarRow label="Unknown" value={analytics.unknown} max={statusMax} color="info.main" />
          <BarRow label="Duplicates" value={analytics.duplicates} max={statusMax} color="secondary.main" />
        </Stack>
        <Stack spacing={0.75} sx={{ flex: 1 }}>
          <Typography variant="caption" sx={{ fontWeight: 700 }}>
            Confidence distribution
          </Typography>
          {analytics.confidenceBuckets.map((bucket) => (
            <BarRow key={bucket.id} label={bucket.label} value={bucket.count} max={confMax} color="primary.main" />
          ))}
        </Stack>
      </Stack>
      <Stack spacing={0.5}>
        <Typography variant="caption" sx={{ fontWeight: 700 }}>
          Recognition progress
        </Typography>
        <Box sx={{ height: 10, borderRadius: 1, bgcolor: "action.hover", overflow: "hidden" }}>
          <Box
            sx={{
              width: `${Math.min(100, Math.max(0, analytics.progressPercent))}%`,
              height: "100%",
              bgcolor: "primary.main",
              transition: (theme) =>
                theme.transitions.create("width", { duration: theme.transitions.duration.short }),
              "@media (prefers-reduced-motion: reduce)": { transition: "none" },
            }}
          />
        </Box>
        <Typography variant="caption" color="text.secondary">
          {Math.round(analytics.progressPercent)}% reviewed
        </Typography>
      </Stack>
    </Stack>
  );
}

export default ReviewAnalyticsDashboard;
