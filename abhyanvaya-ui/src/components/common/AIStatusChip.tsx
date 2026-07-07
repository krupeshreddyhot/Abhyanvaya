import BlockOutlinedIcon from "@mui/icons-material/BlockOutlined";
import CancelOutlinedIcon from "@mui/icons-material/CancelOutlined";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import CloudUploadOutlinedIcon from "@mui/icons-material/CloudUploadOutlined";
import ErrorIcon from "@mui/icons-material/Error";
import HourglassEmptyOutlinedIcon from "@mui/icons-material/HourglassEmptyOutlined";
import PendingOutlinedIcon from "@mui/icons-material/PendingOutlined";
import PeopleIcon from "@mui/icons-material/People";
import RadioButtonUncheckedOutlinedIcon from "@mui/icons-material/RadioButtonUncheckedOutlined";
import RateReviewOutlinedIcon from "@mui/icons-material/RateReviewOutlined";
import TaskAltOutlinedIcon from "@mui/icons-material/TaskAltOutlined";
import { Chip, type ChipProps } from "@mui/material";
import type { ReactElement } from "react";
import { AI_STATUS_LABELS, AIStatus, type AIStatus as AIStatusType } from "../../types/aiWorkflow";

export type AIStatusChipProps = {
  status: AIStatusType;
  variant?: ChipProps["variant"];
  size?: ChipProps["size"];
  label?: string;
};

type StatusVisual = {
  color: ChipProps["color"];
  icon: ReactElement;
};

const STATUS_VISUALS: Record<AIStatusType, StatusVisual> = {
  [AIStatus.Ready]: { color: "success", icon: <CheckCircleIcon /> },
  [AIStatus.Uploading]: { color: "info", icon: <CloudUploadOutlinedIcon /> },
  [AIStatus.Processing]: { color: "info", icon: <HourglassEmptyOutlinedIcon /> },
  [AIStatus.Matching]: { color: "info", icon: <PeopleIcon /> },
  [AIStatus.AwaitingReview]: { color: "warning", icon: <RateReviewOutlinedIcon /> },
  [AIStatus.Completed]: { color: "success", icon: <TaskAltOutlinedIcon /> },
  [AIStatus.Failed]: { color: "error", icon: <ErrorIcon /> },
  [AIStatus.Cancelled]: { color: "default", icon: <CancelOutlinedIcon /> },
  [AIStatus.Pending]: { color: "warning", icon: <PendingOutlinedIcon /> },
  [AIStatus.NotStarted]: { color: "default", icon: <RadioButtonUncheckedOutlinedIcon /> },
  [AIStatus.NotCreated]: { color: "default", icon: <BlockOutlinedIcon /> },
};

export const AIStatusChip = ({
  status,
  variant = "filled",
  size = "small",
  label,
}: AIStatusChipProps) => {
  const visual = STATUS_VISUALS[status];

  return (
    <Chip
      label={label ?? AI_STATUS_LABELS[status]}
      color={visual.color}
      variant={variant}
      size={size}
      icon={visual.icon}
    />
  );
};

export default AIStatusChip;
