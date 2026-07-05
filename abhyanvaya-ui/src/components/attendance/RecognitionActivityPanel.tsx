import HistoryOutlinedIcon from "@mui/icons-material/HistoryOutlined";
import {
  Box,
  Card,
  CardContent,
  Collapse,
  List,
  ListItem,
  ListItemText,
  Stack,
  Typography,
} from "@mui/material";
import type { RecognitionActivityEntry } from "../../types/liveSessionStatus";
import { formatRecognitionActivityTime } from "../../utils/sessionStatusMapper";

export type RecognitionActivityPanelProps = {
  entries: RecognitionActivityEntry[];
  maxItems?: number;
};

export const RecognitionActivityPanel = ({
  entries,
  maxItems = 100,
}: RecognitionActivityPanelProps) => {
  const visibleEntries = entries.slice(0, maxItems);

  return (
    <Card variant="outlined" aria-label="Recognition activity log">
      <CardContent sx={{ py: 2 }}>
        <Stack spacing={1.5}>
          <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
            <HistoryOutlinedIcon color="primary" aria-hidden />
            <Typography variant="subtitle1" component="h3" sx={{ fontWeight: 700 }}>
              AI Activity
            </Typography>
          </Stack>

          <Box
            sx={{
              maxHeight: 240,
              overflowY: "auto",
              border: 1,
              borderColor: "divider",
              borderRadius: 1,
              bgcolor: "background.default",
            }}
          >
            {visibleEntries.length === 0 ? (
              <Typography variant="body2" color="text.secondary" sx={{ p: 2 }}>
                Activity will appear here as recognition progresses.
              </Typography>
            ) : (
              <List dense disablePadding>
                {visibleEntries.map((entry, index) => (
                  <Collapse in key={entry.id} timeout={300 * Math.min(index + 1, 4)}>
                    <ListItem
                      sx={{
                        borderBottom: 1,
                        borderColor: "divider",
                        "@media (prefers-reduced-motion: no-preference)": {
                          animation: index === 0 ? "slideIn 0.35s ease-out" : "none",
                          "@keyframes slideIn": {
                            from: { opacity: 0, transform: "translateY(-6px)" },
                            to: { opacity: 1, transform: "translateY(0)" },
                          },
                        },
                      }}
                    >
                      <ListItemText
                        primary={entry.message}
                        secondary={formatRecognitionActivityTime(entry.timestamp)}
                        slotProps={{
                          primary: { variant: "body2" },
                          secondary: { variant: "caption", sx: { fontFamily: "monospace" } },
                        }}
                      />
                    </ListItem>
                  </Collapse>
                ))}
              </List>
            )}
          </Box>
        </Stack>
      </CardContent>
    </Card>
  );
};

export default RecognitionActivityPanel;
