import { useCallback, useEffect, useState } from "react";
import { Link as RouterLink } from "react-router-dom";
import {
  Alert,
  Button,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import {
  getConflictRuleThresholdHistory,
  getConflictRuleThresholds,
  updateConflictRuleThreshold,
  type ConflictRuleThresholdDto,
} from "../../../../services/schedulingService";

const ConflictRuleThresholdsPage = () => {
  const [rows, setRows] = useState<ConflictRuleThresholdDto[]>([]);
  const [history, setHistory] = useState<
    { thresholdKey: string; oldValue: number; newValue: number; version: number; changeReason?: string; changedUtc: string }[]
  >([]);
  const [drafts, setDrafts] = useState<Record<string, string>>({});
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const load = useCallback(async () => {
    setError(null);
    try {
      const [t, h] = await Promise.all([getConflictRuleThresholds(), getConflictRuleThresholdHistory()]);
      setRows(t.data);
      setHistory(h.data);
      setDrafts(Object.fromEntries(t.data.map((r) => [r.thresholdKey, String(r.value)])));
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to load thresholds");
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const save = async (key: string) => {
    setMessage(null);
    setError(null);
    try {
      await updateConflictRuleThreshold({
        thresholdKey: key,
        value: Number(drafts[key]),
        changeReason: reason || undefined,
      });
      setMessage(`Updated ${key}`);
      await load();
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Update failed");
    }
  };

  return (
    <Stack spacing={2}>
      <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />}>
          Hub
        </Button>
        <Typography variant="h5">Conflict Rule Thresholds</Typography>
      </Stack>
      <Alert severity="info">
        Rules are unchanged — only threshold values are configurable. Database overrides appsettings. Audited with version
        history.
      </Alert>
      {error && <Alert severity="error">{error}</Alert>}
      {message && <Alert severity="success">{message}</Alert>}
      <TextField
        size="small"
        label="Change reason (audit)"
        value={reason}
        onChange={(e) => setReason(e.target.value)}
        sx={{ maxWidth: 420 }}
      />
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Threshold</TableCell>
            <TableCell>Value</TableCell>
            <TableCell>Unit</TableCell>
            <TableCell>Source</TableCell>
            <TableCell>Version</TableCell>
            <TableCell />
          </TableRow>
        </TableHead>
        <TableBody>
          {rows.map((r) => (
            <TableRow key={r.thresholdKey}>
              <TableCell>
                <Typography variant="subtitle2">{r.displayName}</Typography>
                <Typography variant="caption" color="text.secondary">
                  {r.description}
                </Typography>
              </TableCell>
              <TableCell>
                <TextField
                  size="small"
                  value={drafts[r.thresholdKey] ?? ""}
                  onChange={(e) => setDrafts((d) => ({ ...d, [r.thresholdKey]: e.target.value }))}
                  sx={{ width: 120 }}
                />
              </TableCell>
              <TableCell>{r.unit}</TableCell>
              <TableCell>{r.source}</TableCell>
              <TableCell>{r.version}</TableCell>
              <TableCell>
                <Button size="small" variant="contained" onClick={() => void save(r.thresholdKey)}>
                  Save
                </Button>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
      <Typography variant="h6">Version history</Typography>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Key</TableCell>
            <TableCell>Old</TableCell>
            <TableCell>New</TableCell>
            <TableCell>Version</TableCell>
            <TableCell>Reason</TableCell>
            <TableCell>When (UTC)</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {history.map((h, i) => (
            <TableRow key={`${h.thresholdKey}-${h.version}-${i}`}>
              <TableCell>{h.thresholdKey}</TableCell>
              <TableCell>{h.oldValue}</TableCell>
              <TableCell>{h.newValue}</TableCell>
              <TableCell>{h.version}</TableCell>
              <TableCell>{h.changeReason}</TableCell>
              <TableCell>{h.changedUtc}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </Stack>
  );
};

export default ConflictRuleThresholdsPage;
