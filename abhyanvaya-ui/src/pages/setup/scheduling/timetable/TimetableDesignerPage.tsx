import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
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
  IconButton,
  InputLabel,
  List,
  ListItem,
  ListItemButton,
  ListItemText,
  Menu,
  MenuItem,
  Select,
  Stack,
  ToggleButton,
  ToggleButtonGroup,
  Tooltip,
  Typography,
} from "@mui/material";
import { Link as RouterLink, useParams } from "react-router-dom";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import LockIcon from "@mui/icons-material/Lock";
import LockOpenIcon from "@mui/icons-material/LockOpen";
import RedoIcon from "@mui/icons-material/Redo";
import UndoIcon from "@mui/icons-material/Undo";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import ContentPasteIcon from "@mui/icons-material/ContentPaste";
import FileCopyIcon from "@mui/icons-material/FileCopy";
import DeleteIcon from "@mui/icons-material/Delete";
import ArchiveIcon from "@mui/icons-material/Archive";
import PublishIcon from "@mui/icons-material/Publish";
import SendIcon from "@mui/icons-material/Send";
import EditIcon from "@mui/icons-material/Edit";
import { useAuth } from "../../../../context/AuthContext";
import { PermissionKeys } from "../../../../auth/permissionKeys";
import {
  archiveTimetable,
  dismissTimetableSoftWarning,
  getTimetableSoftWarnings,
  publishTimetable,
  submitTimetableForReview,
  type SoftWarningDto,
} from "../../../../services/schedulingService";
import {
  bulkTimetableEntries,
  copyTimetableEntry,
  createTimetableEntry,
  deleteTimetableEntry,
  getTimetableGrid,
  listSubjectAllocations,
  lockTimetable,
  moveTimetableEntry,
  searchRooms,
  TimetableStatus,
  unlockTimetable,
  type SubjectAllocationDto,
  type TimetableEntryDto,
  type TimetableGridDto,
} from "../../../../services/schedulingService";
import SoftWarningsPanel from "./SoftWarningsPanel";
import { DAY_LABELS, errMsg, parseOptionalSelectNumber } from "../schedulingFormUtils";
import TimetableEntryDialog from "./TimetableEntryDialog";
import TimetableGrid, { type TimetableViewMode } from "./TimetableGrid";
import {
  cellKey,
  cellsInRect,
  clearSelection,
  parseCellKey,
  resolveWeekDays,
  type CellCoord,
  type CellSelection,
} from "./timetableSelection";
import { listStaff } from "../../../../services/setupService";
import { useTimetableHistory } from "./useTimetableHistory";

import { periodTimeSlots, TIMETABLE_STATUS_COLORS, TIMETABLE_STATUS_LABELS, timetablePrintSx } from "./timetableUtils";

const buildCellWarningCounts = (warnings: SoftWarningDto[]): Map<string, number> => {
  const map = new Map<string, number>();
  for (const w of warnings) {
    if (w.dismissed || w.dayOfWeek == null || w.timeSlotId == null) continue;
    const key = cellKey(w.dayOfWeek, w.timeSlotId);
    map.set(key, (map.get(key) ?? 0) + 1);
  }
  return map;
};
const DND_ALLOCATION = "application/x-timetable-allocation";
const DND_ENTRY = "application/x-timetable-entry";

const TimetableDesignerPage = () => {
  const { id: idParam } = useParams<{ id: string }>();
  const timetableId = Number(idParam);
  const { hasPermission } = useAuth();
  const canManage = hasPermission(PermissionKeys.SchedulingTimetableManage);
  const canPublish = hasPermission(PermissionKeys.SchedulingPublish);
  const canArchive = hasPermission(PermissionKeys.SchedulingArchive);

  const [grid, setGrid] = useState<TimetableGridDto | null>(null);
  const [entries, setEntries] = useState<TimetableEntryDto[]>([]);
  const [allocations, setAllocations] = useState<SubjectAllocationDto[]>([]);
  const [staffOptions, setStaffOptions] = useState<{ id: number; label: string }[]>([]);
  const [roomOptions, setRoomOptions] = useState<{ id: number; label: string }[]>([]);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [dirty, setDirty] = useState(false);
  const [viewMode, setViewMode] = useState<TimetableViewMode>("academic");
  const [filterStaffId, setFilterStaffId] = useState<number | "">("");
  const [filterRoomId, setFilterRoomId] = useState<number | "">("");

  const [selectedCells, setSelectedCells] = useState<CellSelection>(clearSelection());
  const selectionAnchor = useRef<CellCoord | null>(null);
  const isDraggingSelection = useRef(false);
  const clipboard = useRef<TimetableEntryDto[]>([]);

  const [entryDialogOpen, setEntryDialogOpen] = useState(false);
  const [editingEntry, setEditingEntry] = useState<TimetableEntryDto | null>(null);
  const [entryInitial, setEntryInitial] = useState<Partial<{ dayOfWeek: number; timeSlotId: number; subjectAllocationId: number; roomId: number | null }>>();

  const [roomPromptOpen, setRoomPromptOpen] = useState(false);
  const [pendingDrop, setPendingDrop] = useState<{
    allocationId: number;
    coord: CellCoord;
    preferredRoomId: number | null;
  } | null>(null);
  const [promptRoomId, setPromptRoomId] = useState<number | "">("");

  const [contextMenu, setContextMenu] = useState<{
    mouseX: number;
    mouseY: number;
    entry: TimetableEntryDto | null;
    coord: CellCoord | null;
  } | null>(null);

  const [dupDayFrom, setDupDayFrom] = useState<number | "">(1);
  const [dupDayTo, setDupDayTo] = useState<number | "">(2);
  const [softWarnings, setSoftWarnings] = useState<SoftWarningDto[]>([]);
  const [lifecycleBusy, setLifecycleBusy] = useState(false);

  const history = useTimetableHistory();
  const cellWarningCounts = useMemo(() => buildCellWarningCounts(softWarnings), [softWarnings]);
  const activeWarningCount = useMemo(() => softWarnings.filter((w) => !w.dismissed).length, [softWarnings]);

  const isFrozen = !!grid?.timetable.isFrozen;
  const readOnly = grid?.timetable.status !== TimetableStatus.Draft || !canManage || isFrozen;
  const periodSlots = useMemo(() => periodTimeSlots(grid?.timeSlots ?? []), [grid?.timeSlots]);
  const days = useMemo(() => resolveWeekDays(entries), [entries]);
  const slotIds = useMemo(() => periodSlots.map((s) => s.id), [periodSlots]);

  const displayedEntries = useMemo(() => {
    if (viewMode === "faculty" && filterStaffId !== "") {
      return entries.filter((e) => e.staffId === filterStaffId);
    }
    if (viewMode === "room" && filterRoomId !== "") {
      return entries.filter((e) => e.roomId === filterRoomId);
    }
    return entries;
  }, [entries, viewMode, filterStaffId, filterRoomId]);

  const refreshGrid = useCallback(async () => {
    const res = await getTimetableGrid(timetableId);
    setGrid(res.data);
    setEntries(res.data.entries);
    setDirty(false);
  }, [timetableId]);

  const refreshSoftWarnings = useCallback(async () => {
    try {
      const res = await getTimetableSoftWarnings(timetableId);
      setSoftWarnings(res.data);
    } catch {
      // Soft warnings are informational; do not block the designer.
    }
  }, [timetableId]);

  useEffect(() => {
    if (!timetableId) return;
    void (async () => {
      setLoading(true);
      setError(null);
      try {
        await refreshGrid();
        await refreshSoftWarnings();
        const g = await getTimetableGrid(timetableId);
        const yearId = g.data.timetable.academicYearId;
        const [allocRes, staffRes, roomRes] = await Promise.all([
          listSubjectAllocations({ academicYearId: yearId }),
          listStaff({ page: 1, pageSize: 500 }),
          searchRooms({ page: 1, pageSize: 500, isActive: true }),
        ]);
        setAllocations(allocRes.data);
        setStaffOptions(staffRes.data.items.map((s) => ({ id: s.id, label: `${s.firstName} ${s.lastName}` })));
        setRoomOptions(roomRes.data.items.map((r) => ({ id: r.id, label: `${r.code} — ${r.name}` })));
      } catch (e) {
        setError(errMsg(e));
      } finally {
        setLoading(false);
      }
    })();
  }, [timetableId, refreshGrid, refreshSoftWarnings]);

  const upsertEntryLocal = (entry: TimetableEntryDto) => {
    setEntries((prev) => {
      const idx = prev.findIndex((e) => e.id === entry.id);
      if (idx >= 0) {
        const next = [...prev];
        next[idx] = entry;
        return next;
      }
      return [...prev, entry];
    });
  };

  const removeEntryLocal = (entryId: number) => {
    setEntries((prev) => prev.filter((e) => e.id !== entryId));
  };

  const handleDismissWarning = async (warning: SoftWarningDto) => {
    if (!canManage) return;
    try {
      await dismissTimetableSoftWarning(timetableId, {
        code: warning.code,
        entryId: warning.entryId,
        staffId: warning.staffId,
        roomId: warning.roomId,
        dayOfWeek: warning.dayOfWeek,
        timeSlotId: warning.timeSlotId,
      });
      await refreshSoftWarnings();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const handlePublish = async () => {
    setLifecycleBusy(true);
    try {
      const res = await publishTimetable(timetableId);
      setGrid((g) => (g ? { ...g, timetable: res.data } : g));
      await refreshSoftWarnings();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLifecycleBusy(false);
    }
  };

  const handleArchive = async () => {
    setLifecycleBusy(true);
    try {
      const res = await archiveTimetable(timetableId);
      setGrid((g) => (g ? { ...g, timetable: res.data } : g));
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLifecycleBusy(false);
    }
  };

  const handleSubmitForReview = async () => {
    setLifecycleBusy(true);
    try {
      await submitTimetableForReview({ timetableId });
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLifecycleBusy(false);
    }
  };

  const createEntryWithHistory = async (
    payload: Parameters<typeof createTimetableEntry>[1],
    label = "Create entry",
  ) => {
    const res = await createTimetableEntry(timetableId, payload);
    upsertEntryLocal(res.data);
    history.push({
      label,
      undo: async () => {
        await deleteTimetableEntry(res.data.id);
        removeEntryLocal(res.data.id);
      },
      redo: async () => {
        const again = await createTimetableEntry(timetableId, payload);
        upsertEntryLocal(again.data);
      },
    });
    void refreshSoftWarnings();
    return res.data;
  };

  const deleteEntryWithHistory = async (entry: TimetableEntryDto) => {
    await deleteTimetableEntry(entry.id);
    removeEntryLocal(entry.id);
    history.push({
      label: "Delete entry",
      undo: async () => {
        const res = await createTimetableEntry(timetableId, {
          dayOfWeek: entry.dayOfWeek,
          timeSlotId: entry.timeSlotId,
          subjectAllocationId: entry.subjectAllocationId,
          roomId: entry.roomId,
          remarks: entry.remarks,
        });
        upsertEntryLocal(res.data);
      },
      redo: async () => {
        await deleteTimetableEntry(entry.id);
        removeEntryLocal(entry.id);
      },
    });
    void refreshSoftWarnings();
  };

  const moveEntryWithHistory = async (
    entry: TimetableEntryDto,
    target: CellCoord,
    roomId?: number | null,
  ) => {
    const before = { dayOfWeek: entry.dayOfWeek, timeSlotId: entry.timeSlotId, roomId: entry.roomId };
    const res = await moveTimetableEntry(entry.id, {
      dayOfWeek: target.dayOfWeek,
      timeSlotId: target.timeSlotId,
      roomId: roomId ?? entry.roomId,
    });
    upsertEntryLocal(res.data);
    history.push({
      label: "Move entry",
      undo: async () => {
        const back = await moveTimetableEntry(entry.id, before);
        upsertEntryLocal(back.data);
      },
      redo: async () => {
        const again = await moveTimetableEntry(entry.id, {
          dayOfWeek: target.dayOfWeek,
          timeSlotId: target.timeSlotId,
          roomId: roomId ?? entry.roomId,
        });
        upsertEntryLocal(again.data);
      },
    });
    void refreshSoftWarnings();
  };

  const handleDropAllocation = async (coord: CellCoord, allocationId: number, roomId: number | null) => {
    if (readOnly) return;
    const alloc = allocations.find((a) => a.id === allocationId);
    const resolvedRoom = roomId ?? alloc?.preferredRoomId ?? null;
    if (!resolvedRoom) {
      setPendingDrop({ allocationId, coord, preferredRoomId: alloc?.preferredRoomId ?? null });
      setPromptRoomId("");
      setRoomPromptOpen(true);
      return;
    }
    await createEntryWithHistory({
      dayOfWeek: coord.dayOfWeek,
      timeSlotId: coord.timeSlotId,
      subjectAllocationId: allocationId,
      roomId: resolvedRoom,
    });
  };

  const handleDropOnCell = (coord: CellCoord, data: DataTransfer) => {
    const allocRaw = data.getData(DND_ALLOCATION);
    const entryRaw = data.getData(DND_ENTRY);
    if (allocRaw) {
      void handleDropAllocation(coord, Number(allocRaw), null);
      return;
    }
    if (entryRaw) {
      const entry = entries.find((e) => e.id === Number(entryRaw));
      if (entry) void moveEntryWithHistory(entry, coord);
    }
  };

  const handlePaste = async () => {
    if (readOnly || clipboard.current.length === 0 || selectedCells.size === 0) return;
    setDirty(true);
    const targetKeys = [...selectedCells];
    const source = clipboard.current;
    const payloads = targetKeys.slice(0, source.length).map((key, i) => {
      const { dayOfWeek, timeSlotId } = parseCellKey(key);
      const src = source[i % source.length];
      return {
        dayOfWeek,
        timeSlotId,
        subjectAllocationId: src.subjectAllocationId,
        roomId: src.roomId,
        remarks: src.remarks,
      };
    });
    try {
      const res = await bulkTimetableEntries(timetableId, { entries: payloads });
      for (const e of res.data) upsertEntryLocal(e);
      setDirty(false);
    } catch (e) {
      setError(errMsg(e));
      setDirty(false);
    }
  };

  const handleDeleteSelected = async () => {
    if (readOnly) return;
    const toDelete = entries.filter((e) => selectedCells.has(cellKey(e.dayOfWeek, e.timeSlotId)));
    for (const e of toDelete) await deleteEntryWithHistory(e);
    setSelectedCells(clearSelection());
  };

  const handleDuplicateDay = async () => {
    if (readOnly || dupDayFrom === "" || dupDayTo === "") return;
    const from = Number(dupDayFrom);
    const to = Number(dupDayTo);
    const dayEntries = entries.filter((e) => e.dayOfWeek === from);
    for (const e of dayEntries) {
      await copyTimetableEntry(e.id, { targetDayOfWeek: to, targetTimeSlotId: e.timeSlotId, roomId: e.roomId }).then(
        (res) => upsertEntryLocal(res.data),
      );
    }
  };

  const handleLockToggle = async () => {
    if (!grid || !canManage) return;
    try {
      if (grid.timetable.status === TimetableStatus.Draft) {
        const res = await lockTimetable(timetableId);
        setGrid((g) => (g ? { ...g, timetable: res.data } : g));
      } else {
        const res = await unlockTimetable(timetableId);
        setGrid((g) => (g ? { ...g, timetable: res.data } : g));
      }
    } catch (e) {
      setError(errMsg(e));
    }
  };

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        setSelectedCells(clearSelection());
        return;
      }
      if (readOnly) return;
      if (e.key === "Delete") {
        void handleDeleteSelected();
        return;
      }
      if (e.ctrlKey && e.key === "c") {
        const selected = entries.filter((en) => selectedCells.has(cellKey(en.dayOfWeek, en.timeSlotId)));
        if (selected.length) clipboard.current = selected;
        e.preventDefault();
      }
      if (e.ctrlKey && e.key === "v") {
        e.preventDefault();
        void handlePaste();
      }
      if (e.ctrlKey && e.key === "z") {
        e.preventDefault();
        void history.undo();
      }
      if (e.ctrlKey && e.key === "y") {
        e.preventDefault();
        void history.redo();
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  });

  const onCellMouseDown = (coord: CellCoord, event: React.MouseEvent) => {
    if (readOnly) return;
    if (event.shiftKey && selectionAnchor.current) {
      setSelectedCells(new Set(cellsInRect(selectionAnchor.current, coord, days, slotIds)));
    } else {
      selectionAnchor.current = coord;
      isDraggingSelection.current = true;
      setSelectedCells(new Set([cellKey(coord.dayOfWeek, coord.timeSlotId)]));
    }
  };

  const onCellMouseEnter = (coord: CellCoord, event: React.MouseEvent) => {
    if (!isDraggingSelection.current || !selectionAnchor.current || event.buttons !== 1) return;
    setSelectedCells(new Set(cellsInRect(selectionAnchor.current, coord, days, slotIds)));
  };

  useEffect(() => {
    const up = () => {
      isDraggingSelection.current = false;
    };
    window.addEventListener("mouseup", up);
    return () => window.removeEventListener("mouseup", up);
  }, []);

  if (loading) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", p: 4 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (!grid) {
    return <Alert severity="error">Timetable not found.</Alert>;
  }

  return (
    <Stack spacing={2} sx={timetablePrintSx}>
      <Box className="no-print" sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup/scheduling/timetables" startIcon={<ArrowBackIcon />} variant="text">
          Timetables
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          {grid.timetable.name}
        </Typography>
        <Chip
          label={TIMETABLE_STATUS_LABELS[grid.timetable.status]}
          color={TIMETABLE_STATUS_COLORS[grid.timetable.status]}
          size="small"
        />
        {isFrozen && <Chip label="Frozen" color="warning" size="small" />}
        {dirty && <Chip label="Unsaved" color="warning" size="small" variant="outlined" />}
        {activeWarningCount > 0 && (
          <Tooltip title="Soft warnings (informational only)">
            <Chip label={`${activeWarningCount} warnings`} color="warning" size="small" variant="outlined" />
          </Tooltip>
        )}
        {canManage && grid.timetable.status === TimetableStatus.Locked && (
          <Button
            size="small"
            startIcon={<SendIcon />}
            disabled={lifecycleBusy}
            onClick={() => void handleSubmitForReview()}
          >
            Submit for review
          </Button>
        )}
        {canPublish && grid.timetable.status === TimetableStatus.Locked && (
          <Button size="small" startIcon={<PublishIcon />} disabled={lifecycleBusy} onClick={() => void handlePublish()}>
            Publish
          </Button>
        )}
        {canArchive && grid.timetable.status !== TimetableStatus.Archived && (
          <Button size="small" startIcon={<ArchiveIcon />} disabled={lifecycleBusy} onClick={() => void handleArchive()}>
            Archive
          </Button>
        )}
        {canManage && (
          <Tooltip title={grid.timetable.status === TimetableStatus.Draft ? "Lock timetable" : "Unlock timetable"}>
            <IconButton onClick={() => void handleLockToggle()} color="primary">
              {grid.timetable.status === TimetableStatus.Draft ? <LockOpenIcon /> : <LockIcon />}
            </IconButton>
          </Tooltip>
        )}
        {!readOnly && (
          <>
            <Tooltip title="Undo (Ctrl+Z)">
              <span>
                <IconButton disabled={!history.canUndo} onClick={() => void history.undo()}>
                  <UndoIcon />
                </IconButton>
              </span>
            </Tooltip>
            <Tooltip title="Redo (Ctrl+Y)">
              <span>
                <IconButton disabled={!history.canRedo} onClick={() => void history.redo()}>
                  <RedoIcon />
                </IconButton>
              </span>
            </Tooltip>
            <Tooltip title="Copy (Ctrl+C)">
              <IconButton
                onClick={() => {
                  clipboard.current = entries.filter((e) =>
                    selectedCells.has(cellKey(e.dayOfWeek, e.timeSlotId)),
                  );
                }}
              >
                <ContentCopyIcon />
              </IconButton>
            </Tooltip>
            <Tooltip title="Paste (Ctrl+V)">
              <IconButton onClick={() => void handlePaste()}>
                <ContentPasteIcon />
              </IconButton>
            </Tooltip>
            <Tooltip title="Delete selected">
              <IconButton onClick={() => void handleDeleteSelected()}>
                <DeleteIcon />
              </IconButton>
            </Tooltip>
          </>
        )}
      </Box>

      {isFrozen && (
        <Alert severity="warning" className="no-print">
          Timetable Frozen — designer is read-only until an Academic Admin unlocks it
          {grid.timetable.freezeReason ? ` (${grid.timetable.freezeReason})` : ""}.
        </Alert>
      )}
      {error && (
        <Alert severity="error" className="no-print" onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      <Stack direction={{ xs: "column", md: "row" }} spacing={2} className="no-print">
        <ToggleButtonGroup
          exclusive
          size="small"
          value={viewMode}
          onChange={(_, v) => v && setViewMode(v)}
        >
          <ToggleButton value="academic">Academic</ToggleButton>
          <ToggleButton value="faculty">Faculty</ToggleButton>
          <ToggleButton value="room">Room</ToggleButton>
        </ToggleButtonGroup>

        {viewMode === "faculty" && (
          <FormControl size="small" sx={{ minWidth: 200 }}>
            <InputLabel>Faculty</InputLabel>
            <Select
              label="Faculty"
              value={filterStaffId}
              onChange={(e) => setFilterStaffId(parseOptionalSelectNumber(e.target.value))}
            >
              <MenuItem value="">Select staff</MenuItem>
              {staffOptions.map((s) => (
                <MenuItem key={s.id} value={s.id}>
                  {s.label}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        )}

        {viewMode === "room" && (
          <FormControl size="small" sx={{ minWidth: 200 }}>
            <InputLabel>Room</InputLabel>
            <Select
              label="Room"
              value={filterRoomId}
              onChange={(e) => setFilterRoomId(parseOptionalSelectNumber(e.target.value))}
            >
              <MenuItem value="">Select room</MenuItem>
              {roomOptions.map((r) => (
                <MenuItem key={r.id} value={r.id}>
                  {r.label}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        )}

        {!readOnly && (
          <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
            <FormControl size="small" sx={{ minWidth: 100 }}>
              <InputLabel>From</InputLabel>
              <Select label="From" value={dupDayFrom} onChange={(e) => setDupDayFrom(parseOptionalSelectNumber(e.target.value))}>
                {days.map((d) => (
                  <MenuItem key={d} value={d}>
                    {DAY_LABELS[d]}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <FormControl size="small" sx={{ minWidth: 100 }}>
              <InputLabel>To</InputLabel>
              <Select label="To" value={dupDayTo} onChange={(e) => setDupDayTo(parseOptionalSelectNumber(e.target.value))}>
                {days.map((d) => (
                  <MenuItem key={d} value={d}>
                    {DAY_LABELS[d]}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <Button size="small" startIcon={<FileCopyIcon />} onClick={() => void handleDuplicateDay()}>
              Duplicate day
            </Button>
          </Stack>
        )}
      </Stack>

      <Box sx={{ display: "flex", gap: 2, flexDirection: { xs: "column", lg: "row" } }}>
        {!readOnly && (
          <Box
            className="no-print"
            sx={{
              width: { xs: "100%", lg: 260 },
              flexShrink: 0,
              border: 1,
              borderColor: "divider",
              borderRadius: 1,
              maxHeight: "calc(100vh - 240px)",
              overflow: "auto",
            }}
          >
            <Typography variant="subtitle2" sx={{ p: 1.5, pb: 0 }}>
              Subject allocations
            </Typography>
            <Typography variant="caption" color="text.secondary" sx={{ px: 1.5, display: "block", mb: 1 }}>
              Drag onto a cell to schedule
            </Typography>
            <List dense disablePadding>
              {allocations.map((a) => (
                <ListItem key={a.id} disablePadding>
                  <ListItemButton
                    draggable
                    onDragStart={(e) => {
                      e.dataTransfer.setData(DND_ALLOCATION, String(a.id));
                      e.dataTransfer.effectAllowed = "copy";
                    }}
                  >
                    <ListItemText
                      primary={`Allocation #${a.id}`}
                      secondary={`Subject ${a.subjectId} · Staff ${a.staffId}`}
                      slotProps={{
                        primary: { sx: { fontSize: "0.85rem" } },
                        secondary: { sx: { fontSize: "0.7rem" } },
                      }}
                    />
                  </ListItemButton>
                </ListItem>
              ))}
            </List>
          </Box>
        )}

        <SoftWarningsPanel
          warnings={softWarnings}
          canDismiss={canManage}
          onDismiss={(w) => void handleDismissWarning(w)}
        />

        <Box sx={{ flex: 1, minWidth: 0 }}>
          <TimetableGrid
            timeSlots={grid.timeSlots}
            entries={displayedEntries}
            days={days}
            readOnly={readOnly}
            viewMode={viewMode}
            cellWarningCounts={cellWarningCounts}
            selectedCells={readOnly ? undefined : selectedCells}
            onCellMouseDown={onCellMouseDown}
            onCellMouseEnter={onCellMouseEnter}
            onCellContextMenu={(coord, e) => {
              if (readOnly) return;
              setContextMenu({ mouseX: e.clientX, mouseY: e.clientY, entry: null, coord });
            }}
            onEntryClick={(entry, e) => {
              if (e.detail === 2) {
                setEditingEntry(entry);
                setEntryDialogOpen(true);
              }
            }}
            onEntryContextMenu={(entry, e) => {
              setContextMenu({ mouseX: e.clientX, mouseY: e.clientY, entry, coord: null });
            }}
            onDropOnCell={handleDropOnCell}
            onEntryDragStart={(entry, e) => {
              e.dataTransfer.setData(DND_ENTRY, String(entry.id));
              e.dataTransfer.effectAllowed = "move";
            }}
          />
        </Box>
      </Box>

      <Menu
        className="no-print"
        open={contextMenu !== null}
        onClose={() => setContextMenu(null)}
        anchorReference="anchorPosition"
        anchorPosition={
          contextMenu ? { top: contextMenu.mouseY, left: contextMenu.mouseX } : undefined
        }
      >
        {contextMenu?.entry ? (
          [
            <MenuItem
              key="edit"
              onClick={() => {
                setEditingEntry(contextMenu.entry);
                setEntryDialogOpen(true);
                setContextMenu(null);
              }}
            >
              <EditIcon fontSize="small" sx={{ mr: 1 }} /> Edit
            </MenuItem>,
            !readOnly && (
              <MenuItem
                key="dup"
                onClick={() => {
                  if (contextMenu.entry) {
                    void copyTimetableEntry(contextMenu.entry.id, {
                      targetDayOfWeek: contextMenu.entry.dayOfWeek,
                      targetTimeSlotId: contextMenu.entry.timeSlotId,
                    }).then((r) => upsertEntryLocal(r.data));
                  }
                  setContextMenu(null);
                }}
              >
                <FileCopyIcon fontSize="small" sx={{ mr: 1 }} /> Duplicate
              </MenuItem>
            ),
            !readOnly && (
              <MenuItem
                key="copy"
                onClick={() => {
                  if (contextMenu.entry) clipboard.current = [contextMenu.entry];
                  setContextMenu(null);
                }}
              >
                <ContentCopyIcon fontSize="small" sx={{ mr: 1 }} /> Copy
              </MenuItem>
            ),
            !readOnly && (
              <MenuItem
                key="del"
                onClick={() => {
                  if (contextMenu.entry) void deleteEntryWithHistory(contextMenu.entry);
                  setContextMenu(null);
                }}
              >
                <DeleteIcon fontSize="small" sx={{ mr: 1 }} /> Delete
              </MenuItem>
            ),
          ]
        ) : (
          !readOnly && (
            <MenuItem
              onClick={() => {
                if (contextMenu?.coord) {
                  setEditingEntry(null);
                  setEntryInitial({
                    dayOfWeek: contextMenu.coord.dayOfWeek,
                    timeSlotId: contextMenu.coord.timeSlotId,
                  });
                  setEntryDialogOpen(true);
                }
                setContextMenu(null);
              }}
            >
              <EditIcon fontSize="small" sx={{ mr: 1 }} /> New entry
            </MenuItem>
          )
        )}
      </Menu>

      <TimetableEntryDialog
        open={entryDialogOpen}
        timetableId={timetableId}
        academicYearId={grid.timetable.academicYearId}
        timeSlots={grid.timeSlots}
        entry={editingEntry}
        initial={entryInitial}
        readOnly={readOnly}
        onClose={() => {
          setEntryDialogOpen(false);
          setEditingEntry(null);
          setEntryInitial(undefined);
        }}
        onSaved={(e) => upsertEntryLocal(e)}
        onDeleted={(id) => removeEntryLocal(id)}
      />

      <Dialog open={roomPromptOpen} onClose={() => setRoomPromptOpen(false)}>
        <DialogTitle>Select room</DialogTitle>
        <DialogContent>
          <FormControl fullWidth sx={{ mt: 1 }}>
            <InputLabel>Room</InputLabel>
            <Select
              label="Room"
              value={promptRoomId}
              onChange={(e) => setPromptRoomId(parseOptionalSelectNumber(e.target.value))}
            >
              {roomOptions.map((r) => (
                <MenuItem key={r.id} value={r.id}>
                  {r.label}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setRoomPromptOpen(false)}>Cancel</Button>
          <Button
            variant="contained"
            onClick={() => {
              if (pendingDrop && promptRoomId !== "") {
                void handleDropAllocation(pendingDrop.coord, pendingDrop.allocationId, Number(promptRoomId));
              }
              setRoomPromptOpen(false);
              setPendingDrop(null);
            }}
          >
            Schedule
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};

export default TimetableDesignerPage;
