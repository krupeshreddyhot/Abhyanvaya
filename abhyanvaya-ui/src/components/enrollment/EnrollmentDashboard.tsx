import { Alert, Box, Snackbar, Stack } from "@mui/material";
import AiSystemStatusCard from "../ai/AiSystemStatusCard";
import EnrollmentConfigurationCard from "../ai/EnrollmentConfigurationCard";
import EnrollmentWorkflowCard from "../ai/EnrollmentWorkflowCard";
import AiTechnologyCard from "../ai/AiTechnologyCard";
import EnrollmentStatistics from "./EnrollmentStatistics";
import EnrollmentReadinessCard from "./EnrollmentReadinessCard";
import EnrollmentBatchGrid from "./EnrollmentBatchGrid";
import EnrollmentInsightsWidgets from "./EnrollmentInsightsWidgets";
import { mapSystemStatusItems } from "./enrollmentMappers";
import { useEnrollmentDashboard } from "../../context/EnrollmentDashboardContext";

type Props = {
  onViewBatch: (batchId: string) => void;
};

const EnrollmentDashboard = ({ onViewBatch }: Props) => {
  const { systemStatus, configuration, error, toast, hideToast } = useEnrollmentDashboard();

  return (
    <Stack spacing={2}>
      {error ? <Alert severity="error">{error}</Alert> : null}

      {systemStatus ? <AiSystemStatusCard items={mapSystemStatusItems(systemStatus)} /> : null}

      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", lg: "2fr 1fr" },
          gap: 2,
          alignItems: "start",
        }}
      >
        {configuration ? <EnrollmentConfigurationCard configuration={configuration} /> : null}
        <Stack spacing={2}>
          <EnrollmentReadinessCard />
          <AiTechnologyCard />
        </Stack>
      </Box>

      <EnrollmentWorkflowCard />
      <EnrollmentInsightsWidgets />
      <EnrollmentStatistics />
      <EnrollmentBatchGrid onViewBatch={onViewBatch} />

      <Snackbar
        open={toast.open}
        autoHideDuration={6000}
        onClose={hideToast}
        message={toast.message}
        anchorOrigin={{ vertical: "bottom", horizontal: "center" }}
      />
    </Stack>
  );
};

export default EnrollmentDashboard;
