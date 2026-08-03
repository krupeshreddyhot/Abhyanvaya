import { useMemo, useState } from "react";
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import {
  VersionDifferenceCategory,
  VersionDifferenceKind,
  compareScheduleVersions,
  exportVersionComparisonExcel,
  type ScheduleVersionDto,
  type VersionComparisonDto,
  type VersionDifferenceDto,
} from "../../../../services/schedulingService";
import { errMsg, parseOptionalSelectNumber } from "../schedulingFormUtils";

type Props = {
  open: boolean;
  versions: ScheduleVersionDto[];
  canExport: boolean;
  onClose: () => void;
};

const kindColor = (kind: VersionDifferenceKind) =>
  kind === VersionDifferenceKind.Added ? "success" : kind === VersionDifferenceKind.Removed ? "error" : "warning";

const CompareVersionsDialog = ({ open, versions, canExport, onClose }: Props) => {
  const [leftId, setLeftId] = useState<number | "">("");
  const [rightId, setRightId] = useState<number | "">("");
  const [search, setSearch] = useState("");
  const [kindFilter, setKindFilter] = useState<VersionDifferenceKind | "">("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<VersionComparisonDto | null>(null);

  const groups = useMemo(() => {
    if (!result) return [] as [string, VersionDifferenceDto[]][];
    return Object.entries(result.grouped);
  }, [result]);

  const runCompare = async () => {
    if (leftId === "" || rightId === "") {
      setError("Select left and right versions.");
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const res = await compareScheduleVersions({
        leftVersionId: Number(leftId),
        rightVersionId: Number(rightId),
        search: search || null,
        kindFilter: kindFilter === "" ? null : kindFilter,
      });
      setResult(res.data);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  };

  const exportExcel = async () => {
    if (leftId === "" || rightId === "") return;
    try {
      const res = await exportVersionComparisonExcel({
        leftVersionId: Number(leftId),
        rightVersionId: Number(rightId),
        search: search || null,
        kindFilter: kindFilter === "" ? null : kindFilter,
      });
      const url = URL.createObjectURL(res.data as Blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = "version-comparison.xlsx";
      a.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const printResults = () => window.print();

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="md">
      <DialogTitle>Compare versions</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{mt: 1}}>
          {error && <Alert severity="error">{error}</Alert>}
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <FormControl fullWidth>
              <InputLabel>Left version</InputLabel>
              <Select
                label="Left version"
                value={leftId}
                onChange={(e) => setLeftId(parseOptionalSelectNumber(e.target.value))}
              >
                {versions.map((v) => (
                  <MenuItem key={v.id} value={v.id}>
                    {v.versionName} (#{v.versionNumber})
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <FormControl fullWidth>
              <InputLabel>Right version</InputLabel>
              <Select
                label="Right version"
                value={rightId}
                onChange={(e) => setRightId(parseOptionalSelectNumber(e.target.value))}
              >
                {versions.map((v) => (
                  <MenuItem key={v.id} value={v.id}>
                    {v.versionName} (#{v.versionNumber})
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          </Stack>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <TextField fullWidth label="Search" value={search} onChange={(e) => setSearch(e.target.value)} />
            <FormControl fullWidth>
              <InputLabel>Filter kind</InputLabel>
              <Select
                label="Filter kind"
                value={kindFilter}
                onChange={(e) => setKindFilter(e.target.value as VersionDifferenceKind | "")}
              >
                <MenuItem value="">All</MenuItem>
                <MenuItem value={VersionDifferenceKind.Added}>Added</MenuItem>
                <MenuItem value={VersionDifferenceKind.Modified}>Modified</MenuItem>
                <MenuItem value={VersionDifferenceKind.Removed}>Removed</MenuItem>
              </Select>
            </FormControl>
          </Stack>
          <Button variant="contained" onClick={() => void runCompare()} disabled={loading}>
            Generate comparison
          </Button>
          {loading && <CircularProgress size={24} />}
          {result && (
            <Box>
              <Stack direction="row" spacing={1} useFlexGap sx={{ mb: 2, flexWrap: "wrap" }}>
                <Chip color="success" label={`Added ${result.summary.added}`} />
                <Chip color="warning" label={`Modified ${result.summary.modified}`} />
                <Chip color="error" label={`Removed ${result.summary.removed}`} />
                <Chip label={`Faculty ${result.summary.facultyChanges}`} />
                <Chip label={`Rooms ${result.summary.roomChanges}`} />
                <Chip label={`Subjects ${result.summary.subjectChanges}`} />
              </Stack>
              <Typography variant="subtitle2" sx={{mb: 1}}>
                {result.leftVersionName} vs {result.rightVersionName}
              </Typography>
              {groups.map(([name, items]) => (
                <Accordion key={name} defaultExpanded={name === VersionDifferenceCategory.FacultyAssignment.toString()}>
                  <AccordionSummary expandIcon={<ExpandMoreIcon />}>
                    <Typography>
                      {name} ({items.length})
                    </Typography>
                  </AccordionSummary>
                  <AccordionDetails>
                    <Stack spacing={1}>
                      {items.map((d, idx) => (
                        <Box key={`${d.leftEntryId}-${d.rightEntryId}-${idx}`} sx={{borderLeft: 4, borderColor: `${kindColor(d.kind)}.main`, pl: 1}}>
                          <Chip size="small" color={kindColor(d.kind)} label={Object.keys(VersionDifferenceKind).find((k) => VersionDifferenceKind[k as keyof typeof VersionDifferenceKind] === d.kind) ?? d.kind} sx={{ mr: 1 }} />
                          <Typography variant="body2" component="span">
                            {d.summary}
                          </Typography>
                          {(d.leftValue || d.rightValue) && (
                            <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                              {d.leftValue ?? "—"} → {d.rightValue ?? "—"}
                            </Typography>
                          )}
                        </Box>
                      ))}
                    </Stack>
                  </AccordionDetails>
                </Accordion>
              ))}
            </Box>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        {result && (
          <>
            <Button onClick={printResults}>Print</Button>
            {canExport && <Button onClick={() => void exportExcel()}>Export Excel</Button>}
          </>
        )}
        <Button onClick={onClose}>Close</Button>
      </DialogActions>
    </Dialog>
  );
};

export default CompareVersionsDialog;
