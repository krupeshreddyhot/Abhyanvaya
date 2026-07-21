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
      sx={{ alignSelf: { xs: "stretch", sm: "flex-end" }, whiteSpace: "nowrap" }}
    >
      {label}
    </Button>
  );

  return (
    <Stack
      id="enrollment-start-reasons"
      spacing={0.75}
      sx={{
        flexShrink: 0,
        width: { xs: "100%", sm: "auto" },
        maxWidth: { sm: 420 },
        alignItems: { xs: "stretch", sm: "flex-end" },
      }}
    >
      {enabled ? (
        button
      ) : (
        <>
          <Tooltip
            title={
              !canManage
                ? "You do not have permission to start enrollment batches."
                : readiness?.reasons?.join(" ") ?? "Loading readiness..."
            }
          >
            <span style={{ display: "inline-flex", alignSelf: "inherit" }}>{button}</span>
          </Tooltip>
          {readiness?.reasons?.length ? (
            <Typography
              variant="caption"
              color="text.secondary"
              component="ul"
              sx={{
                m: 0,
                pl: { xs: 2, sm: 0 },
                pr: 0,
                textAlign: { xs: "left", sm: "right" },
                listStylePosition: "inside",
              }}
            >
              {readiness.reasons.map((reason: string) => (
                <li key={reason}>{reason}</li>
              ))}
            </Typography>
          ) : null}
        </>
      )}
    </Stack>
  );
};

export default EnrollmentStartButton;
