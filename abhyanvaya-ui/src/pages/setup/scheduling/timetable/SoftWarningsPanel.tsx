import {
  Alert,
  Box,
  Chip,
  IconButton,
  List,
  ListItem,
  ListItemText,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import type { SoftWarningDto } from "../../../../services/schedulingService";

export type SoftWarningsPanelProps = {
  warnings: SoftWarningDto[];
  canDismiss: boolean;
  onDismiss: (warning: SoftWarningDto) => void;
};

const SoftWarningsPanel = ({ warnings, canDismiss, onDismiss }: SoftWarningsPanelProps) => {
  const active = warnings.filter((w) => !w.dismissed);

  return (
    <Box
      sx={{
        width: { xs: "100%", lg: 280 },
        flexShrink: 0,
        border: 1,
        borderColor: "divider",
        borderRadius: 1,
        maxHeight: "calc(100vh - 240px)",
        overflow: "auto",
      }}
    >
      <Stack direction="row" spacing={1} sx={{ p: 1.5, pb: 1, alignItems: "center" }}>
        <WarningAmberIcon color="warning" fontSize="small" />
        <Typography variant="subtitle2" sx={{ flexGrow: 1 }}>
          Soft warnings
        </Typography>
        {active.length > 0 && (
          <Chip label={active.length} size="small" color="warning" variant="outlined" />
        )}
      </Stack>
      <Typography variant="caption" color="text.secondary" sx={{ px: 1.5, display: "block", mb: 1 }}>
        Informational only — editing is never blocked.
      </Typography>

      {active.length === 0 ? (
        <Alert severity="success" sx={{ m: 1.5, py: 0 }}>
          No active warnings
        </Alert>
      ) : (
        <List dense disablePadding>
          {active.map((w, i) => (
            <ListItem
              key={`${w.code}-${w.entryId ?? ""}-${w.dayOfWeek ?? ""}-${w.timeSlotId ?? ""}-${i}`}
              secondaryAction={
                canDismiss ? (
                  <Tooltip title="Dismiss">
                    <IconButton edge="end" size="small" onClick={() => onDismiss(w)}>
                      <CloseIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                ) : undefined
              }
              sx={{ alignItems: "flex-start" }}
            >
              <ListItemText
                primary={
                  <Stack direction="row" spacing={0.5} sx={{ alignItems: "center" }}>
                    <Chip label={w.code} size="small" color="warning" variant="outlined" />
                    {w.severity !== "Warning" && (
                      <Typography variant="caption" color="text.secondary">
                        {w.severity}
                      </Typography>
                    )}
                  </Stack>
                }
                secondary={w.message}
                slotProps={{
                  secondary: { sx: { fontSize: "0.75rem", whiteSpace: "normal" } },
                }}
              />
            </ListItem>
          ))}
        </List>
      )}
    </Box>
  );
};

export default SoftWarningsPanel;
