import { Chip } from "@mui/material";
import { academicChipSx, academicStatusChipColor } from "./academicUiTokens";

export type AcademicStatusChipProps = {
  label: string;
  status?: string | null;
  /** Override automatic color mapping */
  color?: "default" | "success" | "warning" | "error" | "info" | "primary";
  variant?: "filled" | "outlined";
};

/**
 * AI29.1D Prompt 17 — densified status chip aligned with AI31 KPI chips.
 */
export default function AcademicStatusChip({
  label,
  status,
  color,
  variant = "filled",
}: AcademicStatusChipProps) {
  return (
    <Chip
      size="small"
      label={label}
      color={color ?? academicStatusChipColor(status ?? label)}
      variant={variant}
      sx={academicChipSx}
    />
  );
}
