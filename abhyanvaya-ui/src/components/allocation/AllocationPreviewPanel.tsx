import { useMemo, useState, type ReactNode } from "react";
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
  AllocationComparisonReport,
  AllocationExecutionResult,
  AllocationSandboxItem,
} from "../../services/allocationPlatformService";
import CapacityViolationBanner from "./CapacityViolationBanner";
import {
  buildAllocationPreviewRows,
  buildAllocationPreviewSummary,
} from "../../utils/allocationPreviewSummary";
import {
  getExecutionErrors,
  getExecutionTraceSteps,
  getExecutionWarnings,
} from "../../utils/allocationExecutionResultAccessors";
import { ACADEMIC_UI_PAGE_SIZES } from "../../utils/academicRequest";
import { academicTouchButtonSx } from "../academic/academicUiTokens";
import { sanitizeAdministratorMessage } from "../../utils/allocationAdministratorCopy";
import { groupingLabel } from "../../utils/allocationStrategyCatalog";

type EligibleStudent = {
  studentId: number;
  studentNumber?: string | null;
  studentName?: string | null;
  currentSectionCode?: string | null;
};

type Props = {
  result: AllocationExecutionResult | null;
  groupingMode: string;
  /** Window of eligible students for unallocated fill rows (may be capped). */
  eligibleStudents: readonly EligibleStudent[];
  /** Authoritative match count for summary when eligibleStudents is windowed. */
  eligibleStudentCount?: number;
  loading?: boolean;
  canRun?: boolean;
  comparison?: AllocationComparisonReport | null;
  draft?: AllocationSandboxItem | null;
  readinessStrip?: ReactNode;
  showTechnicalDetails?: boolean;
  onPreview: () => void;
  onSimulation: () => void;
  onCompare: () => void;
  onBack: () => void;
  onSaveDraft: (name: string) => void;
};

/**
 * AI29.1D.24B — Allocation Preview over existing execution result.
 * Does not invent explanations or commit StudentSection changes.
 * AI29.1D.24B.4A.2 — Preview and Test Allocation share this panel + summary builders.
 */
const AllocationPreviewPanel = ({
  result,
  groupingMode,
  eligibleStudents,
  eligibleStudentCount,
  loading,
  canRun,
  comparison,
  draft,
  readinessStrip,
  showTechnicalDetails = false,
  onPreview,
  onSimulation,
  onCompare,
  onBack,
  onSaveDraft,
}: Props) => {
  const [draftName, setDraftName] = useState("");
  const totalEligible = eligibleStudentCount ?? eligibleStudents.length;

  const summary = useMemo(
    () =>
      buildAllocationPreviewSummary(result, {
        totalEligibleStudents: totalEligible || undefined,
        groupingMode,
      }),
    [result, totalEligible, groupingMode],
  );

  const rows = useMemo(
    () =>
      buildAllocationPreviewRows(result, {
        groupingMode,
        eligibleStudents: [...eligibleStudents],
        maxRows: ACADEMIC_UI_PAGE_SIZES.allocationPreviewRows,
      }),
    [result, groupingMode, eligibleStudents],
  );

  const scenarioId = result?.scenarioId;
  const errors = getExecutionErrors(result);
  const warnings = getExecutionWarnings(result);
  const traceSteps = getExecutionTraceSteps(result);
  const missingScenarioShape = Boolean(result && !result.scenario);

  return (
    <Stack spacing={1.5}>
      <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
        Allocation Preview
      </Typography>

      <Alert severity="info">
        Preview shows how students would be distributed across sections. Student records are not changed until a later
        approved processing step.
      </Alert>

      {readinessStrip}

      <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
        <Button variant="contained" onClick={onPreview} disabled={!canRun || loading} sx={academicTouchButtonSx}>
          Preview
        </Button>
        <Button variant="outlined" onClick={onSimulation} disabled={!canRun || loading} sx={academicTouchButtonSx}>
          Test Allocation
        </Button>
        <Button variant="outlined" onClick={onCompare} disabled={!scenarioId || loading} sx={academicTouchButtonSx}>
          Compare
        </Button>
        <Button variant="outlined" onClick={onBack} disabled={loading} sx={academicTouchButtonSx}>
          Back
        </Button>
        <TextField
          size="small"
          label="Draft name"
          value={draftName}
          onChange={(e) => setDraftName(e.target.value)}
          sx={{ minWidth: 180 }}
          disabled={!scenarioId || loading}
        />
        <Button
          variant="outlined"
          color="secondary"
          onClick={() => onSaveDraft(draftName.trim() || `Draft ${new Date().toLocaleDateString()}`)}
          disabled={!scenarioId || loading}
          sx={academicTouchButtonSx}
        >
          Save Draft
        </Button>
      </Stack>

      {!result && (
        <Alert severity="warning">
          No allocation result yet. Use <strong>Preview</strong> or <strong>Test Allocation</strong> to see the proposed
          distribution.
        </Alert>
      )}

      {missingScenarioShape && (
        <Alert severity="warning">
          The allocation response did not include scenario details. Try Preview or Test Allocation again.
        </Alert>
      )}

      {summary && (
        <Box
          sx={{
            p: 1.5,
            borderRadius: 1,
            border: "1px solid",
            borderColor: "divider",
          }}
        >
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
            Allocation Summary
          </Typography>
          <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
            <Chip label={`Total Students ${summary.totalStudents}`} />
            <Chip color="success" label={`Allocated ${summary.allocated}`} />
            <Chip color={summary.unallocated ? "warning" : "default"} label={`Unallocated ${summary.unallocated}`} />
            {(summary.sectionCounts ?? []).map((s) => (
              <Chip key={s.sectionId} label={`${s.sectionCode} ${s.assignedCount}`} />
            ))}
            <Chip
              color={summary.constraints.capacityViolations ? "error" : "default"}
              label={`Capacity Issues ${summary.constraints.capacityViolations}`}
            />
            <Chip
              color={summary.constraints.mandatoryViolations ? "error" : "default"}
              label={`Required Issues ${summary.constraints.mandatoryViolations}`}
            />
            <Chip
              color={summary.constraints.preferredViolations ? "warning" : "default"}
              label={`Warnings ${summary.constraints.preferredViolations + summary.constraints.informationalFindings}`}
            />
            {summary.totalScore != null && (
              <Chip color="primary" label={`Allocation Score ${summary.totalScore}`} />
            )}
          </Stack>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
            Primary rule: {groupingLabel(groupingMode)}
          </Typography>
        </Box>
      )}

      {result && (
        <>
          <CapacityViolationBanner
            constraints={result.scenario?.constraints}
            proposedSummaries={result.scenario?.sectionSummaries}
          />
          {errors.length ? (
            <Alert severity="error">{errors.map(sanitizeAdministratorMessage).join(" · ")}</Alert>
          ) : null}
          {warnings.length ? (
            <Alert severity="warning">{warnings.map(sanitizeAdministratorMessage).join(" · ")}</Alert>
          ) : null}

          {comparison && (
            <Alert severity="info">
              {sanitizeAdministratorMessage(comparison.summary || "Comparison completed.")}
            </Alert>
          )}

          {draft && (
            <Alert severity="success">
              Draft saved: {draft.name}. Student section records were not changed.
            </Alert>
          )}

          <Typography variant="body2" sx={{ fontWeight: 600 }}>
            Proposed assignments
          </Typography>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Student</TableCell>
                <TableCell>Current Section</TableCell>
                <TableCell>Proposed Section</TableCell>
                <TableCell>Reason</TableCell>
                <TableCell>Rule Applied</TableCell>
                <TableCell>Capacity Status</TableCell>
                <TableCell>Allocation Score</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {rows.slice(0, 150).map((r) => (
                <TableRow key={r.studentId} selected={!r.allocated}>
                  <TableCell>
                    {r.studentName}
                    {r.studentNumber ? ` (${r.studentNumber})` : ""}
                  </TableCell>
                  <TableCell>{r.currentSection}</TableCell>
                  <TableCell>{r.proposedSection}</TableCell>
                  <TableCell>{r.allocationReason}</TableCell>
                  <TableCell>{r.strategy}</TableCell>
                  <TableCell>{r.constraintResult}</TableCell>
                  <TableCell>{r.score}</TableCell>
                </TableRow>
              ))}
              {!rows.length && (
                <TableRow>
                  <TableCell colSpan={7}>
                    <Typography variant="body2" color="text.secondary">
                      No proposed assignments were returned for this allocation.
                    </Typography>
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>

          {showTechnicalDetails && traceSteps.length > 0 && (
            <Accordion disableGutters elevation={0} sx={{ border: "1px dashed", borderColor: "divider", borderRadius: 1 }}>
              <AccordionSummary expandIcon={<ExpandMoreIcon />}>
                <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                  View Allocation Details
                </Typography>
              </AccordionSummary>
              <AccordionDetails>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>#</TableCell>
                      <TableCell>Rule</TableCell>
                      <TableCell>Applied</TableCell>
                      <TableCell>Score after</TableCell>
                      <TableCell>Summary</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {traceSteps.map((s) => (
                      <TableRow key={s.order}>
                        <TableCell>{s.order}</TableCell>
                        <TableCell>{s.strategyCode}</TableCell>
                        <TableCell>{s.executed ? "Yes" : s.enabled ? "Skipped" : "Off"}</TableCell>
                        <TableCell>{s.scoreAfter}</TableCell>
                        <TableCell>{s.summary}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </AccordionDetails>
            </Accordion>
          )}
        </>
      )}
    </Stack>
  );
};

export default AllocationPreviewPanel;
