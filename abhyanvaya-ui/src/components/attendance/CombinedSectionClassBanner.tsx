import { Alert, Box, Chip, Stack, Typography } from "@mui/material";
import type { CombinedSectionClassView } from "../../utils/combinedSectionClass";

type Props = {
  view: CombinedSectionClassView;
};

/**
 * Prompt 13 — presents TimetableSections / multi-select sections as one operational class
 * while preserving underlying section membership chips for reporting clarity.
 */
export function CombinedSectionClassBanner({ view }: Props) {
  if (!view.displayTitle || !view.operationalLabel) return null;

  return (
    <Alert severity={view.isCombined ? "info" : "success"} sx={{ py: 1 }}>
      <Stack spacing={1}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
          {view.displayTitle}
        </Typography>
        {view.subtitle ? (
          <Typography variant="body2" color="text.secondary">
            {view.subtitle}
          </Typography>
        ) : null}
        <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.75 }}>
          {view.sectionCodes.map((code) => (
            <Chip key={code} size="small" variant="outlined" label={`Section ${code}`} />
          ))}
          {view.sectionCodes.length === 0 && view.sectionIds.map((id) => (
            <Chip key={id} size="small" variant="outlined" label={`Section #${id}`} />
          ))}
        </Box>
      </Stack>
    </Alert>
  );
}
