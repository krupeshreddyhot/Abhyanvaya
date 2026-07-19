import { useEffect, useState } from "react";
import { Box, Stack, Typography } from "@mui/material";
import AddCircleOutlineIcon from "@mui/icons-material/AddCircleOutlined";
import AppBreadcrumbs from "../../components/common/AppBreadcrumbs";
import AiModuleTabs, { type AiModuleTabDef } from "../../components/ai/AiModuleTabs";
import ContextBanner from "../../components/context/ContextBanner";
import CollegeContextSelector from "../../components/context/CollegeContextSelector";
import EnrollmentErrorBoundary from "../../components/enrollment/EnrollmentErrorBoundary";
import EnrollmentDashboard from "../../components/enrollment/EnrollmentDashboard";
import EnrollmentStartButton from "../../components/enrollment/EnrollmentStartButton";
import EnrollmentBatchWizard from "../../components/enrollment/EnrollmentBatchWizard";
import BatchDetailsDialog from "../../components/enrollment/BatchDetailsDialog";
import { EnrollmentDashboardProvider } from "../../context/EnrollmentDashboardContext";
import { useTenantContext } from "../../context/TenantContextProvider";

const DASHBOARD_TABS: AiModuleTabDef[] = [
  { value: "overview", label: "Overview" },
  { value: "history", label: "History" },
  { value: "failures", label: "Failures" },
  { value: "settings", label: "Settings" },
];

const StudentEnrollmentPage = () => {
  const [activeTab, setActiveTab] = useState("overview");
  const [wizardOpen, setWizardOpen] = useState(false);
  const [detailsBatchId, setDetailsBatchId] = useState<string | null>(null);
  const { needsCollegeSelection, refresh } = useTenantContext();
  const [selectorOpen, setSelectorOpen] = useState(false);

  useEffect(() => {
    setSelectorOpen(needsCollegeSelection);
  }, [needsCollegeSelection]);

  return (
    <EnrollmentDashboardProvider>
      <EnrollmentErrorBoundary>
        <Stack spacing={2}>
          <ContextBanner />
          <AppBreadcrumbs items={[{ label: "AI Center", to: "/ai" }, { label: "Student Enrollment" }]} />

          <Stack
            direction={{ xs: "column", sm: "row" }}
            spacing={2}
            sx={{ justifyContent: "space-between", alignItems: { xs: "flex-start", sm: "flex-end" } }}
          >
            <Box>
              <Typography variant="h5">Student Enrollment</Typography>
              <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, maxWidth: 640 }}>
                Bulk AI enrollment downloads student photographs, validates photos, generates embeddings, and prepares
                students for classroom recognition.
              </Typography>
            </Box>

            <EnrollmentStartButton
              label="Start Enrollment Batch"
              icon={<AddCircleOutlineIcon />}
              onClick={() => setWizardOpen(true)}
            />
          </Stack>

          <AiModuleTabs tabs={DASHBOARD_TABS} value={activeTab} onChange={setActiveTab} />

          {activeTab === "overview" && !needsCollegeSelection ? (
            <EnrollmentDashboard onViewBatch={(id) => setDetailsBatchId(id)} />
          ) : activeTab === "overview" ? (
            <Typography variant="body2" color="text.secondary">
              Select a college context to load the enrollment dashboard.
            </Typography>
          ) : (
            <Typography variant="body2" color="text.secondary">
              Select Overview to manage live enrollment batches. Additional tabs reuse the same API-backed data set.
            </Typography>
          )}

          <EnrollmentBatchWizard open={wizardOpen} onClose={() => setWizardOpen(false)} />
          <BatchDetailsDialog
            open={detailsBatchId !== null}
            batchId={detailsBatchId}
            onClose={() => setDetailsBatchId(null)}
          />
          <CollegeContextSelector
            open={selectorOpen}
            onSelected={() => {
              setSelectorOpen(false);
              void refresh();
            }}
          />
        </Stack>
      </EnrollmentErrorBoundary>
    </EnrollmentDashboardProvider>
  );
};

export default StudentEnrollmentPage;
