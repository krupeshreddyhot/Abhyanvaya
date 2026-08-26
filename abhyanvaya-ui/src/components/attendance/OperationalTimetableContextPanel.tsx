import { Alert, Box, Chip, Stack, Typography } from "@mui/material";
import type { OperationalTimetableContextView } from "../../utils/operationalTimetableContext";

type Props = {
  view: OperationalTimetableContextView;
};

/**
 * Prompt 15 — distinguishes Timetable-derived vs Manual selection context.
 * Consumes /attendance-resolution/current mapping only (no React timetable resolver).
 */
export function OperationalTimetableContextPanel({ view }: Props) {
  return (
    <Alert severity={view.source === "TimetableDerived" ? "success" : "info"} sx={{ py: 1 }}>
      <Stack spacing={1.25}>
        <Box sx={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 1 }}>
          <Chip
            size="small"
            color={view.source === "TimetableDerived" ? "success" : "default"}
            label={
              view.source === "TimetableDerived" ? "Timetable-derived context" : "Manually selected context"
            }
          />
          <Typography variant="body2">{view.banner}</Typography>
        </Box>
        <Box
          sx={{
            display: "grid",
            gridTemplateColumns: { xs: "1fr 1fr", sm: "repeat(3, 1fr)", md: "repeat(5, 1fr)" },
            gap: 1,
          }}
        >
          {view.fields.map((f) => (
            <Box key={f.key} sx={{ minWidth: 0 }}>
              <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                {f.label}
                {f.fromTimetable ? " · timetable" : " · manual"}
              </Typography>
              <Typography variant="body2" noWrap title={f.value ?? undefined} sx={{ fontWeight: 600 }}>
                {f.value ?? "—"}
              </Typography>
            </Box>
          ))}
        </Box>
      </Stack>
    </Alert>
  );
}
