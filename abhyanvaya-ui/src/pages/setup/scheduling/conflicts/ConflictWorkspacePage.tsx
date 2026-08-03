import { useCallback, useEffect, useMemo, useState } from "react";
import { Link as RouterLink, useNavigate } from "react-router-dom";
import {
  Alert,
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  List,
  ListItem,
  ListItemText,
  MenuItem,
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
import RefreshIcon from "@mui/icons-material/Refresh";
import {
  addConflictNote,
  analyzeConflicts,
  getConflictGuidance,
  getEnhancedConflictWorkspace,
  pinConflict,
  saveConflictBookmark,
  type ConflictCategory,
  type ConflictExplanationDto,
  type ConflictResolutionDto,
  type ConflictResultDto,
  type ConflictSeverity,
  type DependencyGraphDto,
  type EnhancedConflictWorkspaceDto,
  type ImpactGraphDto,
} from "../../../../services/schedulingService";

const severityColor = (s: ConflictSeverity) =>
  s === 4 ? "error" : s === 3 ? "warning" : s === 2 ? "info" : "default";

type GroupMode = "none" | "rule" | "department" | "faculty" | "severity" | "room";

const ConflictWorkspacePage = () => {
  const navigate = useNavigate();
  const [data, setData] = useState<EnhancedConflictWorkspaceDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [search, setSearch] = useState("");
  const [category, setCategory] = useState<"" | ConflictCategory>("");
  const [severity, setSeverity] = useState<"" | ConflictSeverity>("");
  const [groupBy, setGroupBy] = useState<GroupMode>("none");
  const [graphOpen, setGraphOpen] = useState(false);
  const [explain, setExplain] = useState<ConflictExplanationDto | null>(null);
  const [resolutions, setResolutions] = useState<ConflictResolutionDto[] | null>(null);
  const [impact, setImpact] = useState<ImpactGraphDto | null>(null);
  const [active, setActive] = useState<ConflictResultDto | null>(null);
  const [noteText, setNoteText] = useState("");

  const load = useCallback(async (reanalyze = false) => {
    setLoading(true);
    setError(null);
    try {
      const res = await getEnhancedConflictWorkspace({
        search: search || undefined,
        category: category === "" ? undefined : category,
        severity: severity === "" ? undefined : severity,
        reanalyze,
      });
      setData(res.data);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to load conflict workspace");
    } finally {
      setLoading(false);
    }
  }, [search, category, severity]);

  useEffect(() => {
    void load(false);
  }, [load]);

  const conflicts = useMemo(() => data?.workspace.conflicts ?? [], [data]);
  const dependency: DependencyGraphDto | undefined = data?.dependencyGraph;

  const groupedEntries = useMemo(() => {
    if (!data || groupBy === "none") return null;
    const map =
      groupBy === "rule"
        ? data.groupedByRule
        : groupBy === "department"
          ? data.groupedByDepartment
          : groupBy === "faculty"
            ? data.groupedByFaculty
            : groupBy === "severity"
              ? data.groupedBySeverity
              : data.groupedByRoom;
    return Object.entries(map);
  }, [data, groupBy]);

  const openCell = (c: ConflictResultDto) => {
    if (c.recommendation?.navigationPath) {
      navigate(c.recommendation.navigationPath);
      return;
    }
    if (c.timetableId) {
      navigate(`/setup/scheduling/timetables/${c.timetableId}?entryId=${c.timetableEntryId ?? ""}`);
    }
  };

  const openGuidance = async (c: ConflictResultDto) => {
    setActive(c);
    setError(null);
    try {
      const res = await getConflictGuidance({
        ruleCode: c.ruleCode,
        timetableEntryId: c.timetableEntryId ?? undefined,
        timetableId: c.timetableId ?? undefined,
      });
      setExplain(res.data.explanation);
      setResolutions(res.data.suggestedResolutions);
      setImpact(res.data.impact);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to load guidance");
    }
  };

  const savePin = async (c: ConflictResultDto) => {
    if (!data?.workspace.summary.runId) return;
    await pinConflict({
      conflictDetectionRunId: data.workspace.summary.runId,
      ruleCode: c.ruleCode,
      timetableEntryId: c.timetableEntryId ?? undefined,
    });
    await load(false);
  };

  const saveNote = async () => {
    if (!active || !data?.workspace.summary.runId || !noteText.trim()) return;
    await addConflictNote({
      conflictDetectionRunId: data.workspace.summary.runId,
      ruleCode: active.ruleCode,
      timetableEntryId: active.timetableEntryId ?? undefined,
      noteText: noteText.trim(),
    });
    setNoteText("");
    await load(false);
  };

  const saveFilterBookmark = async () => {
    await saveConflictBookmark({
      name: `Filter ${new Date().toLocaleString()}`,
      filterJson: JSON.stringify({ search, category, severity, groupBy }),
    });
    await load(false);
  };

  const renderConflictRows = (items: ConflictResultDto[]) =>
    items.map((c, idx) => (
      <TableRow key={`${c.ruleCode}-${c.timetableEntryId}-${idx}`} hover>
        <TableCell>
          <Chip size="small" label={c.severity} color={severityColor(c.severity) as "error" | "warning" | "info" | "default"} />
        </TableCell>
        <TableCell>{c.ruleName}</TableCell>
        <TableCell>{c.description}</TableCell>
        <TableCell>
          <Stack direction="row" spacing={0.5} useFlexGap sx={{ flexWrap: "wrap" }}>
            <Button size="small" onClick={() => void openGuidance(c)}>
              Explain
            </Button>
            <Button size="small" onClick={() => openCell(c)} disabled={!c.timetableId}>
              Open cell
            </Button>
            <Button size="small" onClick={() => void savePin(c)}>
              Pin
            </Button>
          </Stack>
        </TableCell>
      </TableRow>
    ));

  return (
    <Stack spacing={2}>
      <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap", alignItems: "center" }}>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />}>
          Hub
        </Button>
        <Typography variant="h5" sx={{flexGrow: 1}}>
          Conflict Workspace
        </Typography>
        <Button component={RouterLink} to="/setup/scheduling/conflicts/analytics" variant="outlined">
          Analytics
        </Button>
        <Button component={RouterLink} to="/setup/scheduling/conflicts/rules" variant="outlined">
          Rule thresholds
        </Button>
      </Stack>
      <Alert severity="info">
        Detection + advisory guidance only — conflicts never auto-fix or edit the timetable.
      </Alert>
      {error && <Alert severity="error">{error}</Alert>}
      <Stack direction={{ xs: "column", md: "row" }} spacing={1}>
        <TextField size="small" label="Search" value={search} onChange={(e) => setSearch(e.target.value)} />
        <TextField
          select
          size="small"
          label="Category"
          value={category}
          onChange={(e) => setCategory(e.target.value === "" ? "" : (Number(e.target.value) as ConflictCategory))}
          sx={{ minWidth: 140 }}
        >
          <MenuItem value="">All</MenuItem>
          <MenuItem value={1}>Faculty</MenuItem>
          <MenuItem value={2}>Room</MenuItem>
          <MenuItem value={3}>Student</MenuItem>
          <MenuItem value={4}>Calendar</MenuItem>
        </TextField>
        <TextField
          select
          size="small"
          label="Severity"
          value={severity}
          onChange={(e) => setSeverity(e.target.value === "" ? "" : (Number(e.target.value) as ConflictSeverity))}
          sx={{ minWidth: 140 }}
        >
          <MenuItem value="">All</MenuItem>
          <MenuItem value={1}>Information</MenuItem>
          <MenuItem value={2}>Warning</MenuItem>
          <MenuItem value={3}>Error</MenuItem>
          <MenuItem value={4}>Critical</MenuItem>
        </TextField>
        <TextField
          select
          size="small"
          label="Group by"
          value={groupBy}
          onChange={(e) => setGroupBy(e.target.value as GroupMode)}
          sx={{ minWidth: 160 }}
        >
          <MenuItem value="none">None</MenuItem>
          <MenuItem value="rule">Rule</MenuItem>
          <MenuItem value="department">Department</MenuItem>
          <MenuItem value="faculty">Faculty</MenuItem>
          <MenuItem value="severity">Severity</MenuItem>
          <MenuItem value="room">Room</MenuItem>
        </TextField>
        <Button variant="outlined" onClick={() => void load(false)} disabled={loading}>
          Filter
        </Button>
        <Button variant="outlined" onClick={() => void saveFilterBookmark()}>
          Save filter
        </Button>
        <Button variant="outlined" onClick={() => setGraphOpen(true)}>
          Dependency graph
        </Button>
        <Button
          variant="contained"
          startIcon={<RefreshIcon />}
          onClick={() =>
            void (async () => {
              await analyzeConflicts({});
              await load(true);
            })()
          }
          disabled={loading}
        >
          Re-analyze
        </Button>
      </Stack>

      {data && (
        <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
          <Chip label={`Total ${data.workspace.summary.totalConflicts}`} />
          <Chip label={`Pins ${data.pins.length}`} variant="outlined" />
          <Chip label={`Bookmarks ${data.bookmarks.length}`} variant="outlined" />
          <Chip label={`Notes ${data.notes.length}`} variant="outlined" />
          <Chip label={`Clusters ${dependency?.clusterCount ?? 0}`} variant="outlined" />
        </Stack>
      )}

      <Box sx={{overflowX: "auto"}}>
        {groupedEntries ? (
          groupedEntries.map(([group, items]) => (
            <Box key={group} sx={{ mb: 2 }}>
              <Typography variant="subtitle1" gutterBottom>
                {group} ({items.length})
              </Typography>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Severity</TableCell>
                    <TableCell>Rule</TableCell>
                    <TableCell>Description</TableCell>
                    <TableCell>Actions</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>{renderConflictRows(items)}</TableBody>
              </Table>
            </Box>
          ))
        ) : (
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Severity</TableCell>
                <TableCell>Rule</TableCell>
                <TableCell>Description</TableCell>
                <TableCell>Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {renderConflictRows(conflicts)}
              {!loading && conflicts.length === 0 && (
                <TableRow>
                  <TableCell colSpan={4}>No conflicts match the current filters.</TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        )}
      </Box>

      <Dialog open={!!explain} onClose={() => setExplain(null)} maxWidth="md" fullWidth>
        <DialogTitle>Explain Conflict</DialogTitle>
        <DialogContent dividers>
          {explain && (
            <Stack spacing={1.5}>
              <Typography variant="h6">{explain.ruleName}</Typography>
              <Typography variant="body2"><strong>Category:</strong> {explain.ruleCategory}</Typography>
              <Typography variant="body2"><strong>Severity / Priority:</strong> {explain.severity} / {explain.priority}</Typography>
              <Typography variant="body2"><strong>Rule description:</strong> {explain.ruleDescription}</Typography>
              <Typography variant="body2"><strong>Business reason:</strong> {explain.businessReason}</Typography>
              <Typography variant="body2"><strong>Why triggered:</strong> {explain.whyTriggered}</Typography>
              <Typography variant="body2"><strong>Suggested action:</strong> {explain.suggestedAction}</Typography>
              <Typography variant="body2"><strong>Impact:</strong> {explain.impact}</Typography>
              <Typography variant="caption">References: {explain.references.join(" · ")}</Typography>
              <Divider />
              <Typography variant="subtitle1">Suggested Resolutions (advisory)</Typography>
              <List dense>
                {(resolutions ?? []).flatMap((r) =>
                  r.options.map((o) => (
                    <ListItem key={`${r.recommendationId}-${o.optionCode}`} alignItems="flex-start">
                      <ListItemText
                        primary={`${o.label} · confidence ${Math.round(r.score.confidence * 100)}% · ${r.estimatedResolution ?? ""}`}
                        secondary={o.description}
                      />
                    </ListItem>
                  )),
                )}
              </List>
              {impact && (
                <>
                  <Divider />
                  <Typography variant="subtitle1">Impact Panel · Risk {impact.summary.riskLevel}</Typography>
                  <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
                    <Chip size="small" label={`Faculty ${impact.summary.facultyAffected}`} />
                    <Chip size="small" label={`Students ${impact.summary.studentsAffected}`} />
                    <Chip size="small" label={`Rooms ${impact.summary.roomsAffected}`} />
                    <Chip size="small" label={`Departments ${impact.summary.departmentsAffected}`} />
                    <Chip size="small" label={`Published ${impact.summary.publishedVersionsAffected}`} />
                    <Chip size="small" label={`Attendance ${impact.summary.attendanceSignals}`} />
                  </Stack>
                  <List dense>
                    {impact.nodes.map((n) => (
                      <ListItem key={n.nodeId}>
                        <ListItemText primary={n.label} secondary={n.detail ?? undefined} />
                      </ListItem>
                    ))}
                  </List>
                </>
              )}
              <TextField
                label="Conflict note"
                value={noteText}
                onChange={(e) => setNoteText(e.target.value)}
                fullWidth
                multiline
                minRows={2}
              />
            </Stack>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => void saveNote()} disabled={!noteText.trim()}>
            Save note
          </Button>
          {active && (
            <Button onClick={() => openCell(active)} disabled={!active.timetableId}>
              Open timetable cell
            </Button>
          )}
          <Button onClick={() => setExplain(null)}>Close</Button>
        </DialogActions>
      </Dialog>

      <Dialog open={graphOpen} onClose={() => setGraphOpen(false)} maxWidth="md" fullWidth>
        <DialogTitle>Conflict Dependency Graph</DialogTitle>
        <DialogContent dividers>
          <Typography variant="body2" gutterBottom>
            Clusters: {dependency?.clusterCount ?? 0} · Edges: {dependency?.edgeCount ?? 0} · Roots:{" "}
            {dependency?.rootConflictCount ?? 0}
          </Typography>
          <Box component="pre" sx={{whiteSpace: "pre-wrap", fontSize: 12, bgcolor: "grey.100", p: 1, borderRadius: 1}}>
            {dependency?.mermaid || "No graph data"}
          </Box>
          <List dense>
            {(dependency?.nodes ?? []).map((n) => (
              <ListItem
                key={n.nodeId}
                secondaryAction={
                  <Button
                    size="small"
                    disabled={!n.navigationPath}
                    onClick={() => n.navigationPath && navigate(n.navigationPath)}
                  >
                    Open cell
                  </Button>
                }
              >
                <ListItemText primary={n.label} secondary={`${n.ruleCode} · cluster ${n.clusterKey}`} />
              </ListItem>
            ))}
          </List>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setGraphOpen(false)}>Close</Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};

export default ConflictWorkspacePage;
