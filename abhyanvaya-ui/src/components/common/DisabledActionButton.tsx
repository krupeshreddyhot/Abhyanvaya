import { Button, Tooltip, type ButtonProps } from "@mui/material";
import type { ReactNode } from "react";

export type DisabledActionButtonProps = {
  label: string;
  tooltip: string;
  icon?: ReactNode;
  size?: ButtonProps["size"];
  variant?: ButtonProps["variant"];
  fullWidth?: boolean;
};

/**
 * Reusable "coming soon" primary action (AI20.UI.10's hero CTA and AI20.UI.11's empty-state CTA
 * both use this). Wraps the disabled `Button` in a `<span>` inside `Tooltip` — the MUI-recommended
 * workaround, since a disabled native button fires no pointer/focus events and would otherwise
 * never trigger the tooltip.
 */
const DisabledActionButton = ({
  label,
  tooltip,
  icon,
  size = "medium",
  variant = "contained",
  fullWidth,
}: DisabledActionButtonProps) => (
  <Tooltip title={tooltip}>
    <span style={fullWidth ? { display: "block", width: "100%" } : undefined}>
      <Button variant={variant} size={size} startIcon={icon} disabled fullWidth={fullWidth} sx={{ px: 3 }}>
        {label}
      </Button>
    </span>
  </Tooltip>
);

export default DisabledActionButton;
