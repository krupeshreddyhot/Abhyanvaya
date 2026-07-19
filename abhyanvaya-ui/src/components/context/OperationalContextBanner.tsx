import { Alert, Box, Button, Chip, Paper, Stack, Typography } from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import SwapHorizIcon from "@mui/icons-material/SwapHoriz";
import { useMemo } from "react";
import { useTenantContext } from "../../context/TenantContextProvider";

type Props = {
  onChangeContext?: () => void;
};

const formatAge = (createdUtc?: string | null, expiresUtc?: string | null) => {
  if (!createdUtc) return "—";
  const created = new Date(createdUtc);
  const ageMinutes = Math.floor((Date.now() - created.getTime()) / 60000);
  if (expiresUtc) {
    const remaining = Math.max(0, Math.floor((new Date(expiresUtc).getTime() - Date.now()) / 60000));
    return `${ageMinutes}m old · ${remaining}m remaining`;
  }
  return `${ageMinutes}m old`;
};

const OperationalContextBanner = ({ onChangeContext }: Props) => {
  const { context, isSuperAdmin, hasOperationalContext, clearOperationalContext, loading, renewOperationalContext } =
    useTenantContext();

  const contextTypeLabel = useMemo(() => {
    if (!context) return "Unknown";
    if (context.isGlobal) return "Global";
    return "College";
  }, [context]);

  if (loading) {
    return null;
  }

  if (isSuperAdmin && !hasOperationalContext) {
    return (
      <Alert severity="warning" role="status">
        No operational college context. Select a college to load tenant-scoped data. Your login session is unchanged.
      </Alert>
    );
  }

  if (!context?.selectedCollegeId && !isSuperAdmin) {
    return null;
  }

  const collegeLabel = context?.selectedCollegeName
    ? `${context.selectedCollegeName} (${context.selectedCollegeCode ?? context.selectedCollegeId})`
    : context?.isGlobal
      ? "Global scope"
      : "College scope";

  return (
    <Paper variant="outlined" sx={{ p: 1.5 }} role="status">
      <Stack direction={{ xs: "column", md: "row" }} spacing={1} sx={{ alignItems: { md: "center" }, justifyContent: "space-between" }}>
        <Box>
          <Typography variant="caption" color="text.secondary">
            Operational context
          </Typography>
          <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", mt: 0.5 }}>
            <Chip size="small" label={`College: ${collegeLabel}`} color="primary" variant="outlined" />
            <Chip size="small" label={`Tenant: ${context?.tenantId ?? 0}`} variant="outlined" />
            <Chip size="small" label={`Type: ${contextTypeLabel}`} variant="outlined" />
            <Chip size="small" label={`Age: ${formatAge(context?.createdUtc, context?.expiresUtc)}`} variant="outlined" />
          </Stack>
        </Box>
        {isSuperAdmin ? (
          <Stack direction="row" spacing={1}>
            <Button size="small" startIcon={<SwapHorizIcon />} onClick={onChangeContext}>
              Change
            </Button>
            <Button size="small" onClick={() => void renewOperationalContext()}>
              Renew
            </Button>
            {!context?.isGlobal ? (
              <Chip
                size="small"
                label="Clear"
                onClick={() => void clearOperationalContext()}
                onDelete={() => void clearOperationalContext()}
                deleteIcon={<CloseIcon />}
                variant="outlined"
              />
            ) : null}
          </Stack>
        ) : null}
      </Stack>
    </Paper>
  );
};

export default OperationalContextBanner;
