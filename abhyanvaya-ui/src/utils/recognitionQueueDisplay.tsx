import AccessTimeOutlinedIcon from "@mui/icons-material/AccessTimeOutlined";
import AutorenewOutlinedIcon from "@mui/icons-material/AutorenewOutlined";
import CancelOutlinedIcon from "@mui/icons-material/CancelOutlined";
import CheckCircleOutlinedIcon from "@mui/icons-material/CheckCircleOutlined";
import CloudUploadOutlinedIcon from "@mui/icons-material/CloudUploadOutlined";
import FaceOutlinedIcon from "@mui/icons-material/FaceOutlined";
import HourglassEmptyOutlinedIcon from "@mui/icons-material/HourglassEmptyOutlined";
import PeopleOutlinedIcon from "@mui/icons-material/PeopleOutlined";
import PlayCircleOutlinedIcon from "@mui/icons-material/PlayCircleOutlined";
import QueueOutlinedIcon from "@mui/icons-material/QueueOutlined";
import RateReviewOutlinedIcon from "@mui/icons-material/RateReviewOutlined";
import SaveOutlinedIcon from "@mui/icons-material/SaveOutlined";
import WarningAmberOutlinedIcon from "@mui/icons-material/WarningAmberOutlined";
import { Chip, type ChipProps } from "@mui/material";
import type { ReactElement } from "react";
import { BackendRecognitionQueueStatus } from "../types/liveSessionStatus";

export type RecognitionQueueVisual = {
  label: string;
  description: string;
  color: ChipProps["color"];
  icon: ReactElement;
};

const QUEUE_VISUALS: Record<number, RecognitionQueueVisual> = {
  [BackendRecognitionQueueStatus.Waiting]: {
    label: "Waiting",
    description: "Upload a classroom photo to begin",
    color: "default",
    icon: <HourglassEmptyOutlinedIcon />,
  },
  [BackendRecognitionQueueStatus.Queued]: {
    label: "Queued",
    description: "Waiting for recognition worker",
    color: "info",
    icon: <QueueOutlinedIcon />,
  },
  [BackendRecognitionQueueStatus.WorkerPicked]: {
    label: "Worker Picked",
    description: "Background worker started processing",
    color: "info",
    icon: <PlayCircleOutlinedIcon />,
  },
  [BackendRecognitionQueueStatus.Detecting]: {
    label: "Detecting",
    description: "AI is detecting faces in the photo",
    color: "primary",
    icon: <FaceOutlinedIcon />,
  },
  [BackendRecognitionQueueStatus.Matching]: {
    label: "Matching",
    description: "Matching detected faces to enrolled students",
    color: "primary",
    icon: <PeopleOutlinedIcon />,
  },
  [BackendRecognitionQueueStatus.Saving]: {
    label: "Saving",
    description: "Saving recognition results",
    color: "primary",
    icon: <SaveOutlinedIcon />,
  },
  [BackendRecognitionQueueStatus.AwaitingReview]: {
    label: "Awaiting Review",
    description: "Ready for teacher review",
    color: "warning",
    icon: <RateReviewOutlinedIcon />,
  },
  [BackendRecognitionQueueStatus.Completed]: {
    label: "Completed",
    description: "Recognition pipeline finished",
    color: "success",
    icon: <CheckCircleOutlinedIcon />,
  },
  [BackendRecognitionQueueStatus.Failed]: {
    label: "Failed",
    description: "Recognition failed",
    color: "error",
    icon: <WarningAmberOutlinedIcon />,
  },
  [BackendRecognitionQueueStatus.Cancelled]: {
    label: "Cancelled",
    description: "Session was cancelled",
    color: "default",
    icon: <CancelOutlinedIcon />,
  },
};

export const getRecognitionQueueVisual = (queueStatus: number): RecognitionQueueVisual =>
  QUEUE_VISUALS[queueStatus] ?? {
    label: "Processing",
    description: "Recognition in progress",
    color: "info",
    icon: <AutorenewOutlinedIcon />,
  };

export const RecognitionQueueChip = ({ queueStatus }: { queueStatus: number }) => {
  const visual = getRecognitionQueueVisual(queueStatus);
  return (
    <Chip
      label={visual.label}
      color={visual.color}
      size="small"
      icon={visual.icon}
      sx={{
        fontWeight: 600,
        "& .MuiChip-icon": { fontSize: 18 },
        transition: (theme) =>
          theme.transitions.create(["transform", "background-color"], { duration: 300 }),
        "@media (prefers-reduced-motion: no-preference)": {
          animation: queueStatus >= 1 && queueStatus <= 5 ? "queuePulse 2s ease-in-out infinite" : "none",
          "@keyframes queuePulse": {
            "0%, 100%": { transform: "scale(1)" },
            "50%": { transform: "scale(1.02)" },
          },
        },
      }}
    />
  );
};

export const RecognitionQueueUploadIcon = CloudUploadOutlinedIcon;
export const RecognitionQueueTimeIcon = AccessTimeOutlinedIcon;
