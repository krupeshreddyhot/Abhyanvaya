import { useState } from "react";
import { Box, Stack, Typography } from "@mui/material";
import AddCircleOutlineIcon from "@mui/icons-material/AddCircleOutlined";
import AiModuleTabs, { type AiModuleTabDef } from "../../components/ai/AiModuleTabs";
import EnrollmentErrorBoundary from "../../components/enrollment/EnrollmentErrorBoundary";
import EnrollmentDashboard from "../../components/enrollment/EnrollmentDashboard";
import EnrollmentStartButton from "../../components/enrollment/EnrollmentStartButton";
import EnrollmentBatchWizard from "../../components/enrollment/EnrollmentBatchWizard";
import BatchDetailsDialog from "../../components/enrollment/BatchDetailsDialog";
import { EnrollmentDashboardProvider } from "../../context/EnrollmentDashboardContext";
import ContextAwareLayout from "../../layouts/ContextAwareLayout";

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

  return (
    <EnrollmentDashboardProvider>
      <EnrollmentErrorBoundary>
        <ContextAwareLayout breadcrumbItems={[{ label: "AI Center", to: "/ai" }, { label: "Student Enrollment" }]}>
          <Stack
            direction={{ xs: "column", sm: "row" }}
            spacing={2}
            sx={{
              justifyContent: "space-between",
              alignItems: { xs: "stretch", sm: "flex-start" },
              gap: 2,
              mb: 0.5,
            }}
          >
            <Box sx={{ flex: 1, minWidth: 0 }}>
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

          {activeTab === "overview" ? (
            <EnrollmentDashboard onViewBatch={(id) => setDetailsBatchId(id)} />
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
        </ContextAwareLayout>
      </EnrollmentErrorBoundary>
    </EnrollmentDashboardProvider>
  );
};

export default StudentEnrollmentPage;
