import { Box, Paper, Stack, Typography } from "@mui/material";
import type { AttendanceRecognitionReviewDto, AttendanceRecognitionReviewHistoryDto, AttendanceSessionReviewDto, AuditEntryDto, RecognitionSummaryDto } from "../../services/attendanceRecognitionService";
import type { RecognitionReviewFilter } from "../../utils/recognitionReviewFilters";
import { ClassroomPhotoPanel } from "./ClassroomPhotoPanel";
import { RecognitionCard } from "./RecognitionCard";
import { RecognitionReviewFilterBar } from "./RecognitionReviewFilterBar";
import { RecognitionReviewTimeline } from "./RecognitionReviewTimeline";
import { RecognitionSummaryCard } from "./RecognitionSummaryCard";
import { SelectedFaceDetailsPanel } from "./SelectedFaceDetailsPanel";
import { VirtualizedRecognitionList } from "./VirtualizedRecognitionList";

const LIST_ITEM_HEIGHT = 132;
const LIST_VIEWPORT_HEIGHT = 520;

export type RecognitionReviewPanelProps = {
  session: AttendanceSessionReviewDto | null;
  summary: RecognitionSummaryDto | null;
  recognitions: AttendanceRecognitionReviewDto[];
  filteredRecognitions: AttendanceRecognitionReviewDto[];
  history: AttendanceRecognitionReviewHistoryDto[];
  auditEntries?: AuditEntryDto[];
  focusedId: string | null;
  selectedIds: Set<string>;
  activeFilters: Set<RecognitionReviewFilter>;
  searchText: string;
  notesById: Record<string, string>;
  isApproved: boolean;
  actionLoading: boolean;
  pendingCount: number;
  selectedCount: number;
  allPendingSelected: boolean;
  somePendingSelected: boolean;
  onSearchChange: (value: string) => void;
  onToggleFilter: (filter: RecognitionReviewFilter) => void;
  onClearFilters: () => void;
  onToggleSelectAllPending: () => void;
  onFocusRecognition: (recognitionId: string) => void;
  onToggleSelected: (recognitionId: string) => void;
  onNotesChange: (recognitionId: string, notes: string) => void;
  onApprove: (recognitionId: string) => void;
  onReject: (recognitionId: string) => void;
  onIgnore: (recognitionId: string) => void;
  onAssign: (recognitionId: string) => void;
};

export function RecognitionReviewPanel({
  session,
  summary,
  recognitions,
  filteredRecognitions,
  history,
  auditEntries = [],
  focusedId,
  selectedIds,
  activeFilters,
  searchText,
  notesById,
  isApproved,
  actionLoading,
  pendingCount,
  selectedCount,
  allPendingSelected,
  somePendingSelected,
  onSearchChange,
  onToggleFilter,
  onClearFilters,
  onToggleSelectAllPending,
  onFocusRecognition,
  onToggleSelected,
  onNotesChange,
  onApprove,
  onReject,
  onIgnore,
  onAssign,
}: RecognitionReviewPanelProps) {
  const classroomImageUrl = session?.annotatedImageUrl ?? session?.originalImageUrl ?? null;
  const focusedRecognition =
    recognitions.find((row) => row.recognitionId === focusedId) ?? null;

  return (
    <Stack spacing={2}>
      <RecognitionSummaryCard
        statistics={summary?.statistics ?? null}
        canFinalize={summary?.canFinalize ?? false}
      />

      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: {
            xs: "1fr",
            lg: "minmax(260px, 28%) minmax(0, 1fr) minmax(280px, 30%)",
          },
          gap: 2,
          alignItems: "start",
        }}
      >
        <ClassroomPhotoPanel
          imageUrl={classroomImageUrl}
          imageWidth={session?.imageWidth ?? null}
          imageHeight={session?.imageHeight ?? null}
          recognitions={recognitions}
          highlightedRecognitionId={focusedId}
          onHighlightRecognition={(recognitionId) => {
            if (recognitionId) {
              onFocusRecognition(recognitionId);
            }
          }}
        />

        <Paper variant="outlined" sx={{ p: 2 }} aria-labelledby="recognition-list-heading">
          <Stack spacing={2}>
            <RecognitionReviewFilterBar
              searchText={searchText}
              onSearchChange={onSearchChange}
              activeFilters={activeFilters}
              onToggleFilter={onToggleFilter}
              onClearFilters={onClearFilters}
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
                height={LIST_VIEWPORT_HEIGHT}
                getKey={(row) => row.recognitionId}
                renderItem={(row) => (
                  <RecognitionCard
                    recognition={row}
                    selected={selectedIds.has(row.recognitionId)}
                    focused={focusedId === row.recognitionId}
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
      </Box>

      <RecognitionReviewTimeline history={history} auditEntries={auditEntries} />
    </Stack>
  );
}
