import { Button, Tooltip, type ButtonProps } from "@mui/material";
import type { ReactNode } from "react";
import { missingPermissionTooltip } from "../../auth/academicPermissionAccess";

export type PermissionAwareButtonProps = Omit<ButtonProps, "disabled"> & {
  /** When false, button is disabled with a permission tooltip (UX only). */
  allowed: boolean;
  /** Existing permission key string, e.g. Section.Create */
  permissionKey: string;
  children: ReactNode;
  /** Additional disable reason (scope not ready, loading, etc.) */
  disabled?: boolean;
  disabledTooltip?: string;
};

/**
 * AI29.1D Prompt 18 — disable actions when JWT lacks permission.
 * Server authorization remains authoritative; a 401/403 from the API must still be surfaced by callers.
 */
export default function PermissionAwareButton({
  allowed,
  permissionKey,
  children,
  disabled = false,
  disabledTooltip,
  ...buttonProps
}: PermissionAwareButtonProps) {
  const blockedByPermission = !allowed;
  const isDisabled = blockedByPermission || disabled;
  const tip = blockedByPermission
    ? missingPermissionTooltip(permissionKey)
    : disabled
      ? (disabledTooltip ?? "Action unavailable")
      : "";

  const button = (
    <Button {...buttonProps} disabled={isDisabled}>
      {children}
    </Button>
  );

  if (!isDisabled) return button;

  return (
    <Tooltip title={tip}>
      <span style={{ display: "inline-flex" }}>{button}</span>
    </Tooltip>
  );
}
