import { useState } from "react";
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Alert,
  Box,
  Button,
  Chip,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import type {
  AllocationGovernanceResult,
  AllocationMultiCompareReport,
  AllocationScenarioDetailDto,
} from "../../services/allocationOperationsService";
import type { AllocationComparisonReport, AllocationExecutionResult } from "../../services/allocationPlatformService";
import {
  executionStatusLabel,
  GOVERNANCE_LIFECYCLE_STATES,
  governanceBlockingPresentations,
  governancePrefersRebuild,
  lifecycleChipColor,
  toGovernanceLifecycleDisplay,
} from "../../utils/allocationGovernanceLifecycle";
import {
  APPROVE_CONFIRM_DESCRIPTION,
  APPROVE_CONFIRM_TITLE,
  LABEL_REPLAY_ALLOCATION,
  LABEL_REVIEW_ACADEMIC_SCOPE,
  sanitizeAdministratorMessage,
  versionActionLabel,
} from "../../utils/allocationAdministratorCopy";
import AcademicConfirmDialog from "../academic/AcademicConfirmDialog";
import { academicTouchButtonSx } from "../academic/academicUiTokens";

type Props = {
  scenarioDetail: AllocationScenarioDetailDto | null;
  /** Engine execution status (Completed / Failed / …) — never shown as lifecycle. */
  executionStatus?: string | null;
  executionResult?: AllocationExecutionResult | null;
  governance: AllocationGovernanceResult | null;
  reviewNotes: string;
  rejectReason: string;
  onReviewNotesChange: (v: string) => void;
  onRejectReasonChange: (v: string) => void;
  loading?: boolean;
  canReview?: boolean;
  canApprove?: boolean;
  canReject?: boolean;
  canArchive?: boolean;
  canReplay?: boolean;
  canCompare?: boolean;
  engineCompare?: AllocationComparisonReport | null;
  multiCompare?: AllocationMultiCompareReport | null;
  onRefresh: () => void;
  onReview: () => void;
  onApprove: () => void;
  onReject: () => void;
  onArchive: () => void;
  onReplay: () => void;
  onCompare: () => void;
  /** Navigate to regenerate allocation (existing workflow). */
  onRebuildAllocation?: () => void;
  onBack?: () => void;
  showVersionHistory?: boolean;
  showTechnicalDetails?: boolean;
};

/**
 * AI29.1D.24B — Review / Approve Allocation panel.
 * Approval eligibility comes only from governance.canApprove / blockingReasons.
 */
const AllocationGovernancePanel = ({
  scenarioDetail,
  executionStatus,
  executionResult,
  governance,
  reviewNotes,
  rejectReason,
  onReviewNotesChange,
  onRejectReasonChange,
  loading,
  canReview,
  canApprove,
  canReject,
  canArchive,
  canReplay,
  canCompare,
  engineCompare,
  multiCompare,
  onRefresh,
  onReview,
  onApprove,
  onReject,
  onArchive,
  onReplay,
  onCompare,
  onRebuildAllocation,
  onBack,
  showVersionHistory = true,
  showTechnicalDetails = false,
}: Props) => {
  const [approveConfirmOpen, setApproveConfirmOpen] = useState(false);
  const activeGov = governance ?? scenarioDetail?.governance ?? null;
  const lifecycleRaw = scenarioDetail?.lifecycleStatus;
  const lifecycleDisplay = toGovernanceLifecycleDisplay(lifecycleRaw);
  const execLabel = executionStatusLabel(executionStatus ?? scenarioDetail?.status ?? executionResult?.status);
  const issuePresentations = governanceBlockingPresentations(activeGov);
  const prefersRebuild = governancePrefersRebuild(activeGov) || scenarioDetail?.contextCurrent === false;
  const approveAllowedByGovernance = activeGov?.canApprove === true;
  const approveDisabled = !canApprove || !approveAllowedByGovernance || loading || !scenarioDetail;
  const primaryBlocker = issuePresentations[0];

  const openApproveConfirm = () => {
    if (approveDisabled || approveConfirmOpen) return;
    setApproveConfirmOpen(true);
  };

  const confirmApprove = () => {
    setApproveConfirmOpen(false);
    onApprove();
  };

  return (
    <Stack spacing={1.5}>
      {!scenarioDetail && (
        <Alert severity="warning">Generate or load an allocation first, then review approval status.</Alert>
      )}

      {prefersRebuild && scenarioDetail && (
        <Alert severity="warning">
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5 }}>
            Allocation needs to be rebuilt
          </Typography>
          <Typography variant="body2" sx={{ mb: 1 }}>
            The academic information used for this allocation has changed. Review the academic scope and generate the
            allocation again.
          </Typography>
          <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
            {onRebuildAllocation && (
              <Button variant="contained" color="warning" onClick={onRebuildAllocation} disabled={loading} sx={academicTouchButtonSx}>
                {LABEL_REVIEW_ACADEMIC_SCOPE}
              </Button>
            )}
            {onBack && (
              <Button variant="outlined" onClick={onBack} disabled={loading} sx={academicTouchButtonSx}>
                Back
              </Button>
            )}
          </Stack>
        </Alert>
      )}

      <Box
        sx={{
          p: 1.5,
          borderRadius: 1,
          border: "1px solid",
          borderColor: "divider",
          bgcolor: "action.hover",
        }}
      >
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.75 }}>
          Allocation Status
        </Typography>
        <Stack direction="row" spacing={0.75} useFlexGap sx={{ flexWrap: "wrap" }}>
          {GOVERNANCE_LIFECYCLE_STATES.map((state) => (
            <Chip
              key={state}
              size="small"
              color={state === lifecycleDisplay ? lifecycleChipColor(state) : "default"}
              variant={state === lifecycleDisplay ? "filled" : "outlined"}
              label={state === lifecycleDisplay ? `● ${state}` : `○ ${state}`}
              aria-current={state === lifecycleDisplay ? "step" : undefined}
            />
          ))}
        </Stack>
        <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
          Run status: {execLabel}
          {scenarioDetail ? ` · Version ${scenarioDetail.currentVersionNumber}` : ""}
        </Typography>
      </Box>

      {issuePresentations.length > 0 && (
        <Alert severity="error">
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5 }}>
            Approval Status
          </Typography>
          <Stack spacing={1}>
            {issuePresentations.map((issue) => (
              <Box key={issue.title}>
                <Typography variant="body2" sx={{ fontWeight: 700 }}>
                  {issue.title}
                </Typography>
                <Typography variant="body2">{issue.description}</Typography>
              </Box>
            ))}
          </Stack>
        </Alert>
      )}

      {approveAllowedByGovernance && (
        <Alert severity="success">This allocation is ready for approval.</Alert>
      )}

      <Stack spacing={1}>
        <TextField
          size="small"
          label="Review notes"
          value={reviewNotes}
          onChange={(e) => onReviewNotesChange(e.target.value)}
          fullWidth
          multiline
          minRows={2}
          disabled={!scenarioDetail || loading}
        />
        <TextField
          size="small"
          label="Reject reason"
          value={rejectReason}
          onChange={(e) => onRejectReasonChange(e.target.value)}
          fullWidth
          disabled={!scenarioDetail || loading}
        />
      </Stack>

      <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
        <Button variant="outlined" onClick={onRefresh} disabled={!scenarioDetail || loading} sx={academicTouchButtonSx}>
          Refresh Status
        </Button>
        <Button variant="contained" onClick={onReview} disabled={!canReview || !scenarioDetail || loading} sx={academicTouchButtonSx}>
          Mark as Reviewed
        </Button>
        <Button
          variant="contained"
          color="success"
          onClick={openApproveConfirm}
          disabled={approveDisabled}
          sx={academicTouchButtonSx}
          title={approveDisabled && primaryBlocker ? `${primaryBlocker.title}: ${primaryBlocker.description}` : undefined}
        >
          Approve Allocation
        </Button>
        <Button variant="outlined" color="error" onClick={onReject} disabled={!canReject || !scenarioDetail || loading} sx={academicTouchButtonSx}>
          Reject
        </Button>
        <Button variant="outlined" color="warning" onClick={onArchive} disabled={!canArchive || !scenarioDetail || loading} sx={academicTouchButtonSx}>
          Archive
        </Button>
        <Button variant="outlined" onClick={onReplay} disabled={!canReplay || !scenarioDetail || loading} sx={academicTouchButtonSx}>
          {LABEL_REPLAY_ALLOCATION}
        </Button>
        <Button variant="outlined" onClick={onCompare} disabled={!canCompare || !scenarioDetail || loading} sx={academicTouchButtonSx}>
          Compare
        </Button>
      </Stack>

      {approveDisabled && primaryBlocker && (
        <Typography variant="body2" color="text.secondary">
          Approve Allocation is unavailable: {primaryBlocker.title}. {primaryBlocker.description}
        </Typography>
      )}

      {activeGov?.message && (
        <Alert severity={activeGov.success ? "success" : "info"}>
          {sanitizeAdministratorMessage(activeGov.message)}
        </Alert>
      )}

      {engineCompare && (
        <Alert severity="info">
          Comparison summary: {sanitizeAdministratorMessage(engineCompare.summary || "Completed.")}
        </Alert>
      )}

      {multiCompare && (
        <Alert severity="info">
          {sanitizeAdministratorMessage(multiCompare.summary || "Comparison completed.")}
        </Alert>
      )}

      {showVersionHistory && scenarioDetail && (
        <Box>
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5 }}>
            Version History
          </Typography>
          {(scenarioDetail.versions?.length ?? 0) === 0 ? (
            <Typography variant="body2" color="text.secondary">
              No version history is available for this allocation.
            </Typography>
          ) : (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Version</TableCell>
                  <TableCell>Action</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell>Date</TableCell>
                  <TableCell>Reason</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {[...scenarioDetail.versions]
                  .sort((a, b) => a.versionNumber - b.versionNumber)
                  .map((v) => (
                    <TableRow key={v.versionNumber}>
                      <TableCell>{v.versionNumber}</TableCell>
                      <TableCell>{versionActionLabel(v.operation)}</TableCell>
                      <TableCell>{toGovernanceLifecycleDisplay(v.status)}</TableCell>
                      <TableCell>{v.createdAt ? new Date(v.createdAt).toLocaleString() : "—"}</TableCell>
                      <TableCell>{v.reason || "—"}</TableCell>
                    </TableRow>
                  ))}
              </TableBody>
            </Table>
          )}
        </Box>
      )}

      {showTechnicalDetails && scenarioDetail && (
        <Accordion disableGutters elevation={0} sx={{ border: "1px dashed", borderColor: "divider", borderRadius: 1 }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />} aria-controls="allocation-governance-technical" id="allocation-governance-technical-header">
            <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
              Technical Details
            </Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Stack spacing={0.5}>
              <Typography variant="body2">Allocation reference: {scenarioDetail.scenarioId}</Typography>
              <Typography variant="body2">Context version: {scenarioDetail.contextVersion}</Typography>
              <Typography variant="body2">
                Current context version: {scenarioDetail.currentContextVersion ?? "—"}
              </Typography>
              <Typography variant="body2">Checksum: {scenarioDetail.scenarioChecksum ?? "—"}</Typography>
              <Typography variant="body2">Lifecycle (raw): {lifecycleRaw ?? "—"}</Typography>
              <Typography variant="body2">
                Approval eligible: {activeGov?.canApprove === true ? "yes" : "no"} · Academic scope outdated:{" "}
                {String(activeGov?.contextStale ?? !scenarioDetail.contextCurrent)} · Data integrity flag:{" "}
                {String(activeGov?.checksumInvalid ?? false)}
              </Typography>
            </Stack>
          </AccordionDetails>
        </Accordion>
      )}

      <AcademicConfirmDialog
        open={approveConfirmOpen}
        title={APPROVE_CONFIRM_TITLE}
        description={APPROVE_CONFIRM_DESCRIPTION}
        confirmLabel="Approve Allocation"
        cancelLabel="Cancel"
        confirmColor="primary"
        confirming={Boolean(loading)}
        onCancel={() => {
          if (!loading) setApproveConfirmOpen(false);
        }}
        onConfirm={confirmApprove}
      />
    </Stack>
  );
};

export default AllocationGovernancePanel;
