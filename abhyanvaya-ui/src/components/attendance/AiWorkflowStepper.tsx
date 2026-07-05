import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import RadioButtonUncheckedOutlinedIcon from "@mui/icons-material/RadioButtonUncheckedOutlined";
import {
  Box,
  Fade,
  Grow,
  Step,
  StepConnector,
  stepConnectorClasses,
  StepLabel,
  Stepper,
  type StepIconProps,
} from "@mui/material";
import { styled, useTheme } from "@mui/material/styles";
import useMediaQuery from "@mui/material/useMediaQuery";
import { useEffect, useRef, useState } from "react";
import { AI_ATTENDANCE_WORKFLOW_STEPS } from "../../constants/attendanceConstants";
import { getWorkflowStepIndex, type AIWorkflowStep } from "../../types/aiWorkflow";

export type AiWorkflowStepperProps = {
  currentStep: AIWorkflowStep;
};

const AnimatedConnector = styled(StepConnector)(({ theme }) => ({
  [`&.${stepConnectorClasses.active}`]: {
    [`& .${stepConnectorClasses.line}`]: {
      borderColor: theme.palette.primary.main,
      transition: theme.transitions.create("border-color", {
        duration: theme.transitions.duration.standard,
      }),
    },
  },
  [`&.${stepConnectorClasses.completed}`]: {
    [`& .${stepConnectorClasses.line}`]: {
      borderColor: theme.palette.success.main,
      transition: theme.transitions.create("border-color", {
        duration: theme.transitions.duration.standard,
      }),
    },
  },
  [`& .${stepConnectorClasses.line}`]: {
    transition: theme.transitions.create("border-color", {
      duration: theme.transitions.duration.standard,
    }),
  },
}));

const WorkflowStepIcon = ({ active, completed }: StepIconProps) => {
  if (completed) {
    return (
      <Grow in timeout={300}>
        <Box sx={{ color: "success.main", display: "flex", alignItems: "center" }} aria-hidden>
          <CheckCircleIcon fontSize="small" />
        </Box>
      </Grow>
    );
  }

  if (active) {
    return (
      <Box
        sx={{
          width: 24,
          height: 24,
          borderRadius: "50%",
          bgcolor: "primary.main",
          color: "primary.contrastText",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          fontSize: 12,
          fontWeight: 700,
          animation: "workflowPulse 1.6s ease-in-out infinite",
          "@keyframes workflowPulse": {
            "0%, 100%": { transform: "scale(1)", boxShadow: 0 },
            "50%": { transform: "scale(1.06)", boxShadow: 2 },
          },
        }}
        aria-hidden
      >
        •
      </Box>
    );
  }

  return (
    <Box sx={{ color: "text.disabled", display: "flex", alignItems: "center" }} aria-hidden>
      <RadioButtonUncheckedOutlinedIcon fontSize="small" />
    </Box>
  );
};

export const AiWorkflowStepper = ({ currentStep }: AiWorkflowStepperProps) => {
  const theme = useTheme();
  const isVerticalStepper = useMediaQuery(theme.breakpoints.down("sm"));
  const activeStepIndex = getWorkflowStepIndex(currentStep);
  const previousStepIndex = useRef(activeStepIndex);
  const [fadeIn, setFadeIn] = useState(true);

  useEffect(() => {
    if (previousStepIndex.current === activeStepIndex) {
      return;
    }

    setFadeIn(false);
    const timer = window.setTimeout(() => {
      previousStepIndex.current = activeStepIndex;
      setFadeIn(true);
    }, theme.transitions.duration.short);

    return () => window.clearTimeout(timer);
  }, [activeStepIndex, theme.transitions.duration.short]);

  return (
    <Fade in={fadeIn} timeout={theme.transitions.duration.standard}>
      <Stepper
        activeStep={activeStepIndex}
        orientation={isVerticalStepper ? "vertical" : "horizontal"}
        alternativeLabel={!isVerticalStepper}
        connector={<AnimatedConnector />}
        aria-label="AI attendance workflow"
        sx={{
          mt: 1,
          ...(isVerticalStepper
            ? {}
            : {
                "& .MuiStepLabel-label": {
                  mt: 0.5,
                  transition: theme.transitions.create(["color", "font-weight"], {
                    duration: theme.transitions.duration.standard,
                  }),
                },
                "& .MuiStepLabel-label.Mui-active": {
                  fontWeight: 700,
                },
                "& .MuiStepLabel-label.Mui-completed": {
                  fontWeight: 600,
                },
              }),
        }}
      >
        {AI_ATTENDANCE_WORKFLOW_STEPS.map((step, index) => (
          <Step
            key={step.key}
            completed={index < activeStepIndex}
            active={index === activeStepIndex}
          >
            <StepLabel slots={{ stepIcon: WorkflowStepIcon }}>
              {isVerticalStepper ? step.fullLabel : step.shortLabel}
            </StepLabel>
          </Step>
        ))}
      </Stepper>
    </Fade>
  );
};

export default AiWorkflowStepper;
