import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  List,
  ListItem,
  Stack,
  Typography,
} from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import ErrorIcon from "@mui/icons-material/Error";
import RefreshIcon from "@mui/icons-material/Refresh";
import type { PublishReadinessFindingDto, TimetablePublishReadinessResultDto } from "../../../services/schedulingService";
import { getPublishBlockers } from "./publishReadiness";
import {
  formatFindingContextLine,
  formatFindingMetricsLine,
  publishFindingCodeLabel,
  publishFindingSeverityChipColor,
} from "./publishReadinessPresentation";

export type PublishReadinessPanelProps = {
  readiness: TimetablePublishReadinessResultDto | null;
  loading?: boolean;
  error?: string | null;
  onRecheck?: () => void;
  recheckBusy?: boolean;
  onViewEntry?: (entryId: number) => void;
  /** Optional caption distinguishing from SoftWarnings. */
  showConceptHint?: boolean;
};

/**
 * AI-SCHED-CAP Prompt 8.3 — Presentation of server publish readiness.
 * Does not recalculate capacity/conflicts; filters blockers only via server isBlocking.
 */
const PublishReadinessPanel = ({
  readiness,
  loading = false,
  error = null,
  onRecheck,
  recheckBusy = false,
  onViewEntry,
  showConceptHint = true,
}: PublishReadinessPanelProps) => {
  const blockers = getPublishBlockers(readiness);
  const isReady = readiness?.isReady === true && blockers.length === 0;

  return (
    <Box
      component="section"
      aria-labelledby="publish-readiness-heading"
      sx={{
        width: "100%",
        border: 1,
        borderColor: "divider",
        borderRadius: 1,
        p: 1.5,
      }}
      data-testid="publish-readiness-panel"
    >
      <Stack direction="row" spacing={1} sx={{ alignItems: "center", mb: 1, flexWrap: "wrap" }}>
        <Typography id="publish-readiness-heading" variant="subtitle2" sx={{ flexGrow: 1 }}>
          Publish readiness
        </Typography>
        {onRecheck && (
          <Button
            size="small"
            startIcon={recheckBusy || loading ? <CircularProgress size={14} /> : <RefreshIcon />}
            onClick={() => onRecheck()}
            disabled={recheckBusy || loading}
            aria-label="Re-check publish readiness"
          >
            Re-check
          </Button>
        )}
      </Stack>

      {showConceptHint && (
        <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 1 }}>
          Issues that must be resolved before publication (separate from draft soft warnings).
        </Typography>
      )}

      {loading && !readiness && (
        <Stack direction="row" spacing={1} sx={{ alignItems: "center", py: 1 }} role="status" aria-live="polite">
          <CircularProgress size={18} />
          <Typography variant="body2">Checking publish readiness…</Typography>
        </Stack>
      )}

      {error && !loading && (
        <Alert severity="error" role="alert" sx={{ mb: blockers.length || isReady ? 1 : 0 }}>
          {error}
        </Alert>
      )}

      {!loading && !error && !readiness && (
        <Alert severity="info" role="status">
          Publish readiness has not been evaluated yet. Use Re-check or Publish to get the latest
          server result.
        </Alert>
      )}

      {readiness && isReady && (
        <Alert
          severity="success"
          icon={<CheckCircleIcon fontSize="inherit" />}
          role="status"
          aria-live="polite"
        >
          Ready to publish — no blocking findings from the server.
        </Alert>
      )}

      {readiness && !isReady && (
        <Stack spacing={1}>
          <Alert
            severity="error"
            icon={<ErrorIcon fontSize="inherit" />}
            role="alert"
            aria-live="assertive"
          >
            Cannot publish
            {blockers.length > 0
              ? ` — ${blockers.length} issue${blockers.length === 1 ? "" : "s"} must be resolved before this timetable can be published.`
              : " — the server reported this timetable is not ready."}
          </Alert>

          {blockers.length === 0 ? (
            <Typography variant="body2" color="text.secondary">
              No blocking finding details were returned. Re-check or try Publish again for an updated
              server response.
            </Typography>
          ) : (
            <List dense disablePadding aria-label="Publish blocking findings">
              {blockers.map((f: PublishReadinessFindingDto, i: number) => {
                const context = formatFindingContextLine(f);
                const metrics = formatFindingMetricsLine(f);
                const entryId = f.timetableEntryId;
                const canNavigate = entryId != null && onViewEntry != null;

                return (
                  <ListItem
                    key={`${f.code}-${entryId ?? ""}-${f.timeSlotId ?? ""}-${i}`}
                    alignItems="flex-start"
                    sx={{
                      borderBottom: 1,
                      borderColor: "divider",
                      py: 1.25,
                      display: "block",
                    }}
                  >
                    <Stack spacing={0.75}>
                      <Stack direction="row" spacing={0.5} sx={{ flexWrap: "wrap", alignItems: "center" }}>
                        <Chip
                          size="small"
                          label={publishFindingCodeLabel(f.code)}
                          color={publishFindingSeverityChipColor(f.severity)}
                          variant="outlined"
                        />
                        <Chip size="small" label={f.code} variant="outlined" />
                        {f.severity && (
                          <Typography variant="caption" color="text.secondary">
                            {f.severity}
                          </Typography>
                        )}
                      </Stack>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>
                        {f.title || f.code}
                      </Typography>
                      {f.why && (
                        <Typography variant="caption" color="text.secondary" sx={{ whiteSpace: "normal" }}>
                          Why: {f.why}
                        </Typography>
                      )}
                      {f.recommendedAction && (
                        <Typography variant="caption" color="text.primary" sx={{ whiteSpace: "normal" }}>
                          Action: {f.recommendedAction}
                        </Typography>
                      )}
                      {context && (
                        <Typography variant="caption" color="text.secondary" sx={{ whiteSpace: "normal" }}>
                          {context}
                        </Typography>
                      )}
                      {metrics && (
                        <Typography variant="caption" color="text.secondary" sx={{ whiteSpace: "normal" }}>
                          {metrics}
                        </Typography>
                      )}
                      {canNavigate && (
                        <Box>
                          <Button
                            size="small"
                            onClick={() => onViewEntry(entryId)}
                            aria-label={`View timetable entry ${entryId}`}
                          >
                            View entry
                          </Button>
                        </Box>
                      )}
                    </Stack>
                  </ListItem>
                );
              })}
            </List>
          )}
        </Stack>
      )}
    </Box>
  );
};

export default PublishReadinessPanel;
