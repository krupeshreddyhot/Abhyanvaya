import { Box, Collapse, Paper, Stack, Typography } from "@mui/material";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type {
  AttendanceRecognitionReviewDto,
  AttendanceRecognitionReviewHistoryDto,
  AttendanceSessionReviewDto,
  AuditEntryDto,
  RecognitionSummaryDto,
} from "../../services/attendanceRecognitionService";
import type { AttendanceSessionImage } from "../../types/sessionImage";
import { useThemeManagerOptional } from "../../theme";
import {
  getRelatedRecognitionIds,
  type RecognitionReviewFilter,
} from "../../utils/recognitionReviewFilters";
import type { ReviewAnalyticsSnapshot, SessionProductivityMetrics } from "../../utils/reviewAnalytics";
import type { SmartQueueCategory } from "../../utils/smartReviewQueue";
import { AiActivityPanel } from "./AiActivityPanel";
import { ClassroomPhotoPanel } from "./ClassroomPhotoPanel";
import { ConfidenceHeatMapControls } from "./ConfidenceHeatMapControls";
import { KeyboardShortcutHelpDialog } from "./KeyboardShortcutHelpDialog";
import { RecognitionCard } from "./RecognitionCard";
import { RecognitionReviewFilterBar } from "./RecognitionReviewFilterBar";
import { RecognitionReviewTimeline } from "./RecognitionReviewTimeline";
import { RecognitionSummaryCard } from "./RecognitionSummaryCard";
import { ReviewAnalyticsDashboard } from "./ReviewAnalyticsDashboard";
import { SelectedFaceDetailsPanel } from "./SelectedFaceDetailsPanel";
import { SmartReviewQueueBar } from "./SmartReviewQueueBar";
import { StickyReviewToolbar } from "./StickyReviewToolbar";
import { VirtualizedRecognitionList } from "./VirtualizedRecognitionList";

const LIST_ITEM_HEIGHT = 148;
const LIST_VIEWPORT_HEIGHT = 520;

export type RecognitionReviewPanelProps = {
  session: AttendanceSessionReviewDto | null;
  summary: RecognitionSummaryDto | null;
  recognitions: AttendanceRecognitionReviewDto[];
  filteredRecognitions: AttendanceRecognitionReviewDto[];
  history: AttendanceRecognitionReviewHistoryDto[];
  auditEntries?: AuditEntryDto[];
  sessionImages: AttendanceSessionImage[];
  activeImageSequence: number;
  onActiveImageSequenceChange: (sequence: number) => void;
  onReorderImages?: (orderedIds: string[]) => void;
  focusedId: string | null;
  selectedIds: Set<string>;
  activeFilters: Set<RecognitionReviewFilter>;
  searchText: string;
  hideHighConfidence: boolean;
  notesById: Record<string, string>;
  isApproved: boolean;
  actionLoading: boolean;
  pendingCount: number;
  selectedCount: number;
  allPendingSelected: boolean;
  somePendingSelected: boolean;
  sessionElapsedLabel: string;
  averageReviewLabel: string;
  remainingLabel: string;
  canUndo: boolean;
  canRedo: boolean;
  fullscreen?: boolean;
  heatMapEnabled: boolean;
  heatMapOpacity: number;
  miniMapVisible: boolean;
  smartQueueCategory: SmartQueueCategory | "all";
  smartQueueOnlyPending: boolean;
  smartQueueCounts: Record<SmartQueueCategory, number>;
  smartQueuePendingCount: number;
  smartQueueEstimatedMinutes: number;
  analytics: ReviewAnalyticsSnapshot | null;
  productivity: SessionProductivityMetrics | null;
  onSearchChange: (value: string) => void;
  onToggleFilter: (filter: RecognitionReviewFilter) => void;
  onClearFilters: () => void;
  onHideHighConfidenceChange: (value: boolean) => void;
  onToggleSelectAllPending: () => void;
  onFocusRecognition: (recognitionId: string) => void;
  onToggleSelected: (recognitionId: string) => void;
  onNotesChange: (recognitionId: string, notes: string) => void;
  onApprove: (recognitionId: string) => void;
  onReject: (recognitionId: string) => void;
  onIgnore: (recognitionId: string) => void;
  onAssign: (recognitionId: string) => void;
  onApproveSelected: () => void;
  onRejectSelected: () => void;
  onManualMatchSelected: () => void;
  onMarkUnknownSelected: () => void;
  onUndo: () => void;
  onRedo: () => void;
  onToggleFullscreen?: () => void;
  onHeatMapEnabledChange: (value: boolean) => void;
  onHeatMapOpacityChange: (value: number) => void;
  onSmartQueueCategoryChange: (category: SmartQueueCategory | "all") => void;
  onSmartQueueOnlyPendingChange: (value: boolean) => void;
  shortcutHelpOpen?: boolean;
  onShortcutHelpOpenChange?: (open: boolean) => void;
};

export function RecognitionReviewPanel({
  session,
  summary,
  recognitions,
  filteredRecognitions,
  history,
  auditEntries = [],
  sessionImages,
  activeImageSequence,
  onActiveImageSequenceChange,
  onReorderImages,
  focusedId,
  selectedIds,
  activeFilters,
  searchText,
  hideHighConfidence,
  notesById,
  isApproved,
  actionLoading,
  pendingCount,
  selectedCount,
  allPendingSelected,
  somePendingSelected,
  sessionElapsedLabel,
  averageReviewLabel,
  remainingLabel,
  canUndo,
  canRedo,
  fullscreen = false,
  heatMapEnabled,
  heatMapOpacity,
  miniMapVisible,
  smartQueueCategory,
  smartQueueOnlyPending,
  smartQueueCounts,
  smartQueuePendingCount,
  smartQueueEstimatedMinutes,
  analytics,
  productivity,
  onSearchChange,
  onToggleFilter,
  onClearFilters,
  onHideHighConfidenceChange,
  onToggleSelectAllPending,
  onFocusRecognition,
  onToggleSelected,
  onNotesChange,
  onApprove,
  onReject,
  onIgnore,
  onAssign,
  onApproveSelected,
  onRejectSelected,
  onManualMatchSelected,
  onMarkUnknownSelected,
  onUndo,
  onRedo,
  onToggleFullscreen,
  onHeatMapEnabledChange,
  onHeatMapOpacityChange,
  onSmartQueueCategoryChange,
  onSmartQueueOnlyPendingChange,
  shortcutHelpOpen,
  onShortcutHelpOpenChange,
}: RecognitionReviewPanelProps) {
  const themeManager = useThemeManagerOptional();
  const [photoFlex, setPhotoFlex] = useState(themeManager?.prefs.photoFlex ?? 30);
  const [listFlex, setListFlex] = useState(themeManager?.prefs.listFlex ?? 40);
  const [localHelpOpen, setLocalHelpOpen] = useState(false);
  const helpOpen = shortcutHelpOpen ?? localHelpOpen;
  const setHelpOpen = onShortcutHelpOpenChange ?? setLocalHelpOpen;
  const dragRef = useRef<"photo-list" | "list-details" | null>(null);
  const gridRef = useRef<HTMLDivElement>(null);
  const filmstripHeight = themeManager?.prefs.filmstripHeight ?? 96;

  // AI22.7B 5.5 / 5.8 — persist resizable panel sizes
  useEffect(() => {
    if (!themeManager) {
      return;
    }
    if (
      themeManager.prefs.photoFlex === photoFlex &&
      themeManager.prefs.listFlex === listFlex
    ) {
      return;
    }
    themeManager.updatePrefs({ photoFlex, listFlex });
  }, [photoFlex, listFlex, themeManager]);

  const focusedRecognition =
    recognitions.find((row) => row.recognitionId === focusedId) ?? null;

  const relatedIds = useMemo(
    () => getRelatedRecognitionIds(recognitions, focusedRecognition),
    [recognitions, focusedRecognition],
  );

  const activeImage = useMemo(
    () => sessionImages.find((image) => image.imageSequence === activeImageSequence) ?? null,
    [sessionImages, activeImageSequence],
  );

  const classroomImageUrl =
    activeImage?.imageUrl ??
    session?.annotatedImageUrl ??
    session?.originalImageUrl ??
    null;

  const imageWidth = activeImage?.width ?? session?.imageWidth ?? null;
  const imageHeight = activeImage?.height ?? session?.imageHeight ?? null;

  const onDragStart = useCallback((which: "photo-list" | "list-details") => {
    dragRef.current = which;
  }, []);

  const onDragMove = useCallback((event: React.PointerEvent<HTMLDivElement>) => {
    if (!dragRef.current || !gridRef.current) {
      return;
    }
    const rect = gridRef.current.getBoundingClientRect();
    const x = ((event.clientX - rect.left) / rect.width) * 100;
    if (dragRef.current === "photo-list") {
      setPhotoFlex(Math.min(46, Math.max(18, x)));
    } else {
      const nextList = Math.min(55, Math.max(22, x - photoFlex));
      setListFlex(nextList);
    }
  }, [photoFlex]);

  const onDragEnd = useCallback(() => {
    dragRef.current = null;
  }, []);

  const detailsFlex = Math.max(18, 100 - photoFlex - listFlex);

  return (
    <Stack spacing={2}>
      <StickyReviewToolbar
        pendingCount={pendingCount}
        totalCount={recognitions.length}
        selectedCount={selectedCount}
        sessionElapsedLabel={sessionElapsedLabel}
        averageReviewLabel={averageReviewLabel}
        remainingLabel={remainingLabel}
        canUndo={canUndo}
        canRedo={canRedo}
        disabled={isApproved || actionLoading}
        fullscreen={fullscreen}
        productivity={productivity}
        onApproveSelected={onApproveSelected}
        onRejectSelected={onRejectSelected}
        onManualMatchSelected={onManualMatchSelected}
        onMarkUnknownSelected={onMarkUnknownSelected}
        onUndo={onUndo}
        onRedo={onRedo}
        onToggleFullscreen={onToggleFullscreen}
        onOpenShortcutHelp={() => setHelpOpen(true)}
      />

      <Collapse in={!fullscreen}>
        <Stack spacing={2}>
          <RecognitionSummaryCard
            statistics={summary?.statistics ?? null}
            canFinalize={summary?.canFinalize ?? false}
          />
          {analytics ? (
            <Paper variant="outlined" sx={{ p: 2 }}>
              <ReviewAnalyticsDashboard analytics={analytics} />
            </Paper>
          ) : null}
        </Stack>
      </Collapse>

      <Box
        ref={gridRef}
        onPointerMove={onDragMove}
        onPointerUp={onDragEnd}
        onPointerLeave={onDragEnd}
        sx={{
          display: "grid",
          gridTemplateColumns: {
            xs: "1fr",
            // AI22.7B 5.4 / 5.5 — tablet landscape + desktop adaptive columns
            sm: fullscreen ? "1fr" : "1fr",
            md: `minmax(220px, ${photoFlex}%) 6px minmax(0, ${listFlex}%) 6px minmax(200px, ${detailsFlex}%)`,
            lg: `minmax(200px, ${photoFlex}%) 6px minmax(0, ${listFlex}%) 6px minmax(220px, ${detailsFlex}%)`,
            xl: `minmax(260px, ${photoFlex}%) 6px minmax(0, ${listFlex}%) 6px minmax(260px, ${detailsFlex}%)`,
          },
          gap: { xs: 2, md: 0 },
          alignItems: "stretch",
          minHeight: {
            xs: undefined,
            md: fullscreen ? "calc(100vh - 140px)" : 560,
            lg: fullscreen ? "calc(100vh - 160px)" : 640,
            xl: fullscreen ? "calc(100vh - 160px)" : 720,
          },
        }}
      >
        <Box sx={{ minWidth: 0, minHeight: 0 }}>
          <ClassroomPhotoPanel
            imageUrl={classroomImageUrl}
            imageWidth={imageWidth}
            imageHeight={imageHeight}
            recognitions={recognitions}
            relatedRecognitionIds={relatedIds}
            highlightedRecognitionId={focusedId}
            sessionImages={sessionImages}
            activeImageSequence={activeImageSequence}
            onActiveImageSequenceChange={onActiveImageSequenceChange}
            onReorderImages={onReorderImages}
            hideHighConfidence={hideHighConfidence}
            heatMapEnabled={heatMapEnabled}
            heatMapOpacity={heatMapOpacity}
            miniMapVisible={miniMapVisible}
            filmstripHeight={filmstripHeight}
            onHighlightRecognition={(recognitionId) => {
              if (recognitionId) {
                onFocusRecognition(recognitionId);
              }
            }}
          />
        </Box>

        <Box
          onPointerDown={(event) => {
            event.currentTarget.setPointerCapture(event.pointerId);
            onDragStart("photo-list");
          }}
          sx={{
            display: { xs: "none", md: "block" },
            cursor: "col-resize",
            bgcolor: "divider",
            touchAction: "none",
            "&:hover": { bgcolor: "primary.main" },
          }}
          role="separator"
          aria-orientation="vertical"
          aria-label="Resize photo and list panels"
        />

        <Paper
          variant="outlined"
          sx={{ p: 2, minWidth: 0, minHeight: 0 }}
          aria-labelledby="recognition-list-heading"
        >
          <Stack spacing={2}>
            <SmartReviewQueueBar
              counts={smartQueueCounts}
              activeCategory={smartQueueCategory}
              onlyPending={smartQueueOnlyPending}
              pendingCount={smartQueuePendingCount}
              estimatedMinutes={smartQueueEstimatedMinutes}
              onCategoryChange={onSmartQueueCategoryChange}
              onOnlyPendingChange={onSmartQueueOnlyPendingChange}
            />

            <ConfidenceHeatMapControls
              enabled={heatMapEnabled}
              opacity={heatMapOpacity}
              onEnabledChange={onHeatMapEnabledChange}
              onOpacityChange={onHeatMapOpacityChange}
            />

            <RecognitionReviewFilterBar
              searchText={searchText}
              onSearchChange={onSearchChange}
              activeFilters={activeFilters}
              onToggleFilter={onToggleFilter}
              onClearFilters={onClearFilters}
              hideHighConfidence={hideHighConfidence}
              onHideHighConfidenceChange={onHideHighConfidenceChange}
              totalCount={recognitions.length}
              filteredCount={filteredRecognitions.length}
              pendingCount={pendingCount}
              selectedCount={selectedCount}
              allPendingSelected={allPendingSelected}
              somePendingSelected={somePendingSelected}
              selectionDisabled={isApproved}
              onToggleSelectAllPending={onToggleSelectAllPending}
            />

            {filteredRecognitions.length > 0 ? (
              <VirtualizedRecognitionList
                items={filteredRecognitions}
                itemHeight={LIST_ITEM_HEIGHT}
                height={fullscreen ? Math.max(LIST_VIEWPORT_HEIGHT, 640) : LIST_VIEWPORT_HEIGHT}
                getKey={(row) => row.recognitionId}
                scrollToKey={focusedId}
                renderItem={(row) => (
                  <RecognitionCard
                    recognition={row}
                    selected={selectedIds.has(row.recognitionId)}
                    focused={focusedId === row.recognitionId}
                    related={relatedIds.has(row.recognitionId) && focusedId !== row.recognitionId}
                    batchSelected={selectedIds.has(row.recognitionId)}
                    batchSelectionDisabled={isApproved}
                    onSelect={() => onFocusRecognition(row.recognitionId)}
                    onToggleBatchSelect={() => onToggleSelected(row.recognitionId)}
                  />
                )}
              />
            ) : (
              <Typography variant="body2" color="text.secondary" align="center" sx={{ py: 4 }}>
                No recognitions match the current filters.
              </Typography>
            )}
          </Stack>
        </Paper>

        <Box
          onPointerDown={(event) => {
            event.currentTarget.setPointerCapture(event.pointerId);
            onDragStart("list-details");
          }}
          sx={{
            display: { xs: "none", md: "block" },
            cursor: "col-resize",
            bgcolor: "divider",
            touchAction: "none",
            "&:hover": { bgcolor: "primary.main" },
          }}
          role="separator"
          aria-orientation="vertical"
          aria-label="Resize list and details panels"
        />

        <Stack spacing={2} sx={{ minWidth: 0 }}>
          <SelectedFaceDetailsPanel
            recognition={focusedRecognition}
            notes={focusedId ? notesById[focusedId] ?? "" : ""}
            disabled={isApproved}
            actionLoading={actionLoading}
            onNotesChange={(notes) => {
              if (focusedId) {
                onNotesChange(focusedId, notes);
              }
            }}
            onApprove={() => focusedId && onApprove(focusedId)}
            onReject={() => focusedId && onReject(focusedId)}
            onIgnore={() => focusedId && onIgnore(focusedId)}
            onAssign={() => focusedId && onAssign(focusedId)}
          />
          <Collapse in={!fullscreen}>
            <AiActivityPanel history={history} auditEntries={auditEntries} />
          </Collapse>
        </Stack>
      </Box>

      <Collapse in={!fullscreen}>
        <RecognitionReviewTimeline history={history} auditEntries={auditEntries} />
      </Collapse>

      <KeyboardShortcutHelpDialog open={helpOpen} onClose={() => setHelpOpen(false)} />
    </Stack>
  );
}
