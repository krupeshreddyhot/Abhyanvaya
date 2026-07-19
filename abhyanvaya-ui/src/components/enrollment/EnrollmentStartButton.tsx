import { Button, Stack, Tooltip, Typography } from "@mui/material";
import type { ReactNode } from "react";
import { useEnrollmentDashboard } from "../../context/EnrollmentDashboardContext";

type Props = {
  label: string;
  icon?: ReactNode;
  onClick: () => void;
};

const EnrollmentStartButton = ({ label, icon, onClick }: Props) => {
  const { readiness, canManage, loading } = useEnrollmentDashboard();
  const enabled = canManage && readiness?.canStart === true && !loading;

  const button = (
    <Button
      variant="contained"
      startIcon={icon}
      disabled={!enabled}
      onClick={onClick}
      aria-describedby={!enabled ? "enrollment-start-reasons" : undefined}
    >
      {label}
    </Button>
  );

  if (enabled) {
    return button;
  }

  return (
    <Stack spacing={0.5} id="enrollment-start-reasons">
      <Tooltip
        title={
          !canManage
            ? "You do not have permission to start enrollment batches."
            : readiness?.reasons?.join(" ") ?? "Loading readiness..."
        }
      >
        <span>{button}</span>
      </Tooltip>
      {readiness?.reasons?.length ? (
        <Typography variant="caption" color="text.secondary" component="ul" sx={{ m: 0, pl: 2 }}>
          {readiness.reasons.map((reason: string) => (
            <li key={reason}>{reason}</li>
          ))}
        </Typography>
      ) : null}
    </Stack>
  );
};

export default EnrollmentStartButton;
