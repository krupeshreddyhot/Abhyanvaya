import { Box, Card, CardContent, Stack, Typography } from "@mui/material";
import { alpha } from "@mui/material/styles";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import ArrowDownwardIcon from "@mui/icons-material/ArrowDownward";
import CloudDownloadOutlinedIcon from "@mui/icons-material/CloudDownloadOutlined";
import FactCheckOutlinedIcon from "@mui/icons-material/FactCheckOutlined";
import MemoryOutlinedIcon from "@mui/icons-material/MemoryOutlined";
import CloudUploadOutlinedIcon from "@mui/icons-material/CloudUploadOutlined";
import HowToRegOutlinedIcon from "@mui/icons-material/HowToRegOutlined";
import type { ReactNode } from "react";

type WorkflowStep = {
  icon: ReactNode;
  title: string;
  description: string;
};

/**
 * Static, read-only visualization of the enrollment lifecycle (AI20.UI.14). Purely presentational
 * — no progress, no execution, no live state — it exists so an administrator can understand the
 * five-stage pipeline at a glance without reading documentation. Renders as a horizontal process
 * flow on wide screens and stacks vertically on small screens; connectors switch arrow direction
 * to match.
 */
const WORKFLOW_STEPS: WorkflowStep[] = [
  { icon: <CloudDownloadOutlinedIcon />, title: "Download", description: "Fetch photos from ExamBranch" },
  { icon: <FactCheckOutlinedIcon />, title: "Validate", description: "Exactly one clear face" },
  { icon: <MemoryOutlinedIcon />, title: "Embedding", description: "InsightFace 512D" },
  { icon: <CloudUploadOutlinedIcon />, title: "Storage", description: "Cloudflare R2" },
  { icon: <HowToRegOutlinedIcon />, title: "Recognition", description: "Attendance ready" },
];

const StepNode = ({ step }: { step: WorkflowStep }) => (
  <Stack
    spacing={0.75}
    sx={{ alignItems: "center", textAlign: "center", flex: 1, minWidth: 120, px: 0.5 }}
  >
    <Box
      sx={{
        width: 44,
        height: 44,
        borderRadius: "50%",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        color: "primary.main",
        backgroundColor: (theme) => alpha(theme.palette.primary.main, 0.1),
      }}
    >
      {step.icon}
    </Box>
    <Typography variant="body2" sx={{ fontWeight: 600 }}>
      {step.title}
    </Typography>
    <Typography variant="caption" color="text.secondary">
      {step.description}
    </Typography>
  </Stack>
);

const EnrollmentWorkflowCard = () => (
  <Card variant="outlined">
    <CardContent>
      <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 2 }}>
        Enrollment Workflow
      </Typography>
      <Stack
        direction={{ xs: "column", md: "row" }}
        spacing={0}
        sx={{ alignItems: { xs: "stretch", md: "flex-start" } }}
      >
        {WORKFLOW_STEPS.map((step, index) => (
          <Stack
            key={step.title}
            direction={{ xs: "column", md: "row" }}
            spacing={0}
            sx={{ alignItems: "center", flex: 1 }}
          >
            <StepNode step={step} />
            {index < WORKFLOW_STEPS.length - 1 && (
              <Box
                sx={{
                  color: "text.disabled",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  py: { xs: 0.5, md: 0 },
                  px: { md: 0.5 },
                  mt: { md: 2 },
                }}
                aria-hidden="true"
              >
                <ArrowForwardIcon fontSize="small" sx={{ display: { xs: "none", md: "block" } }} />
                <ArrowDownwardIcon fontSize="small" sx={{ display: { xs: "block", md: "none" } }} />
              </Box>
            )}
          </Stack>
        ))}
      </Stack>
    </CardContent>
  </Card>
);

export default EnrollmentWorkflowCard;
