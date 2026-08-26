import { Alert } from "@mui/material";
import { permissionDeniedCopy } from "../../auth/academicPermissionAccess";

export type PermissionDeniedAlertProps = {
  permissionKey: string;
  /** Optional override; defaults to standard denied copy. */
  message?: string;
};

/**
 * AI29.1D Prompt 18 — consistent denied-state presentation.
 * Does not authorize; only explains a missing JWT permission claim for UX.
 */
export default function PermissionDeniedAlert({ permissionKey, message }: PermissionDeniedAlertProps) {
  return (
    <Alert severity="warning" variant="outlined">
      {message ?? permissionDeniedCopy(permissionKey)}
    </Alert>
  );
}
