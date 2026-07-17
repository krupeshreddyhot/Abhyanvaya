import { useState } from "react";
import { Box, Stack, Typography } from "@mui/material";
import AddCircleOutlineIcon from "@mui/icons-material/AddCircleOutlined";
import GroupsOutlinedIcon from "@mui/icons-material/GroupsOutlined";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import HourglassEmptyIcon from "@mui/icons-material/HourglassEmpty";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutlined";
import TodayIcon from "@mui/icons-material/Today";
import Inventory2OutlinedIcon from "@mui/icons-material/Inventory2Outlined";
import AppBreadcrumbs from "../../components/common/AppBreadcrumbs";
import StatCard from "../../components/common/StatCard";
import EmptyStateCard from "../../components/common/EmptyStateCard";
import DisabledActionButton from "../../components/common/DisabledActionButton";
import AiSystemStatusCard, { type AiSystemStatusItem } from "../../components/ai/AiSystemStatusCard";
import AiModuleTabs, { type AiModuleTabDef } from "../../components/ai/AiModuleTabs";
import EnrollmentConfigurationCard from "../../components/ai/EnrollmentConfigurationCard";
import EnrollmentWorkflowCard from "../../components/ai/EnrollmentWorkflowCard";
import AiTechnologyCard from "../../components/ai/AiTechnologyCard";

const PHASE_2_TOOLTIP = "Available in Phase 2.";

/**
 * Mock-only AI subsystem health for this page (AI20.UI.2/UI.5/UI.8). Replaced with a real status
 * feed once the enrollment background worker and health endpoints exist — no other code here
 * changes when that happens, since AiSystemStatusCard only depends on this shape.
 */
const SYSTEM_STATUS_ITEMS: AiSystemStatusItem[] = [
  { label: "Photo Provider", detail: "ExamBranch", status: "ready" },
  { label: "Embedding Engine", detail: "InsightFace", status: "ready" },
  { label: "Recognition Engine", detail: "InsightFace", status: "ready" },
  { label: "Media Storage", detail: "Cloudflare R2", status: "ready" },
  { label: "Background Worker", status: "ready", statusLabel: "Running" },
];

const DASHBOARD_TABS: AiModuleTabDef[] = [
  { value: "overview", label: "Overview" },
  { value: "history", label: "History", disabled: true, disabledReason: "Available in future phase." },
  { value: "failures", label: "Failures", disabled: true, disabledReason: "Available in future phase." },
  { value: "settings", label: "Settings", disabled: true, disabledReason: "Available in future phase." },
];

const SUMMARY_STATS: { label: string; icon: React.ReactNode }[] = [
  { label: "Total Students", icon: <GroupsOutlinedIcon fontSize="small" /> },
  { label: "Embedded", icon: <CheckCircleOutlineIcon fontSize="small" /> },
  { label: "Pending", icon: <HourglassEmptyIcon fontSize="small" /> },
  { label: "Failed", icon: <ErrorOutlineIcon fontSize="small" /> },
  { label: "Processed Today", icon: <TodayIcon fontSize="small" /> },
];

/**
 * Student Enrollment dashboard shell (AI20.UI.2–UI.16). UI layout only — no batch creation, no
 * status polling, no API calls. Every card below renders mock/placeholder data until the
 * enrollment services, repositories, and background worker are implemented in a later milestone.
 * AI20.UI.15 tuned vertical density so everything above "Recent Enrollment Batches" fits on a
 * 1080p viewport, and pairs Configuration + AI Stack side-by-side on large screens.
 */
const StudentEnrollmentPage = () => {
  const [activeTab, setActiveTab] = useState("overview");

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs items={[{ label: "AI Center", to: "/ai" }, { label: "Student Enrollment" }]} />

      <Stack
        direction={{ xs: "column", sm: "row" }}
        spacing={2}
        sx={{ justifyContent: "space-between", alignItems: { xs: "flex-start", sm: "flex-end" } }}
      >
        <Box>
          <Typography variant="h5">Student Enrollment</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, maxWidth: 640 }}>
            Bulk AI enrollment downloads student photographs, validates photos, generates embeddings, and
            prepares students for classroom recognition.
          </Typography>
        </Box>

        {/* AI20.UI.10: primary action leads the page; AI20.UI.15 moved it inline with the header on
            wide screens to reclaim vertical space while keeping it above the fold on mobile. */}
        <Stack spacing={0.25} sx={{ alignItems: { xs: "flex-start", sm: "flex-end" }, flexShrink: 0 }}>
          <DisabledActionButton
            label="Start Enrollment Batch"
            tooltip={PHASE_2_TOOLTIP}
            icon={<AddCircleOutlineIcon />}
          />
          <Typography variant="caption" color="text.secondary">
            {PHASE_2_TOOLTIP}
          </Typography>
        </Stack>
      </Stack>

      <AiSystemStatusCard items={SYSTEM_STATUS_ITEMS} />

      {/* AI20.UI.15: Configuration + AI Stack share a row on large monitors (denser), stack on smaller. */}
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", lg: "2fr 1fr" },
          gap: 2,
          alignItems: "start",
        }}
      >
        <EnrollmentConfigurationCard />
        <AiTechnologyCard />
      </Box>

      <EnrollmentWorkflowCard />

      <AiModuleTabs tabs={DASHBOARD_TABS} value={activeTab} onChange={setActiveTab} />

      {activeTab === "overview" && (
        <Stack spacing={2}>
          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: {
                xs: "1fr 1fr",
                sm: "repeat(3, 1fr)",
                md: "repeat(5, 1fr)",
              },
              gap: 1.5,
            }}
          >
            {SUMMARY_STATS.map((stat) => (
              <StatCard key={stat.label} label={stat.label} value="--" icon={stat.icon} />
            ))}
          </Box>

          <Box>
            <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>
              Recent Enrollment Batches
            </Typography>
            <EmptyStateCard
              icon={<Inventory2OutlinedIcon fontSize="medium" />}
              title="No Enrollment Batches"
              description="Create your first enrollment batch to begin downloading student photographs and generating AI face embeddings."
              action={
                <DisabledActionButton
                  label="Start Enrollment Batch"
                  tooltip={PHASE_2_TOOLTIP}
                  icon={<AddCircleOutlineIcon />}
                  variant="outlined"
                />
              }
            />
          </Box>
        </Stack>
      )}
    </Stack>
  );
};

export default StudentEnrollmentPage;
