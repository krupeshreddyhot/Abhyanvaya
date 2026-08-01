import {
  Box,
  FormControlLabel,
  Slider,
  Stack,
  Switch,
  Typography,
} from "@mui/material";
import { HEATMAP_BANDS } from "../../utils/confidenceHeatMap";

export type ConfidenceHeatMapControlsProps = {
  enabled: boolean;
  opacity: number;
  onEnabledChange: (value: boolean) => void;
  onOpacityChange: (value: number) => void;
};

/** AI22.7A Phase 5.5 — heat map toggle, opacity, and legend. */
export function ConfidenceHeatMapControls({
  enabled,
  opacity,
  onEnabledChange,
  onOpacityChange,
}: ConfidenceHeatMapControlsProps) {
  return (
    <Stack spacing={1} aria-label="Confidence heat map controls">
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center" }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
          Confidence Heat Map
        </Typography>
        <FormControlLabel
          sx={{ m: 0 }}
          control={
            <Switch
              size="small"
              checked={enabled}
              onChange={(event) => onEnabledChange(event.target.checked)}
              slotProps={{ input: { "aria-label": "Toggle confidence heat map" } }}
            />
          }
          label={<Typography variant="caption">{enabled ? "On" : "Off"}</Typography>}
        />
      </Stack>
      {enabled ? (
        <>
          <Typography variant="caption" color="text.secondary">
            Fill opacity
          </Typography>
          <Slider
            size="small"
            min={0.1}
            max={0.85}
            step={0.05}
            value={opacity}
            onChange={(_, value) => onOpacityChange(Number(value))}
            aria-label="Heat map opacity"
            valueLabelDisplay="auto"
          />
          <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", gap: 0.75 }} aria-label="Heat map legend">
            {HEATMAP_BANDS.map((band) => (
              <Stack key={band.id} direction="row" spacing={0.5} sx={{ alignItems: "center" }}>
                <Box
                  aria-hidden
                  sx={{
                    width: 12,
                    height: 12,
                    borderRadius: 0.5,
                    bgcolor: band.color,
                    opacity,
                  }}
                />
                <Typography variant="caption">{band.label}</Typography>
              </Stack>
            ))}
          </Stack>
        </>
      ) : null}
    </Stack>
  );
}

export default ConfidenceHeatMapControls;
