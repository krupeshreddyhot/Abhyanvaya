import { Alert, Box, Button, Chip, Paper, Stack, Typography } from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import SwapHorizIcon from "@mui/icons-material/SwapHoriz";
import RefreshOutlinedIcon from "@mui/icons-material/RefreshOutlined";
import SchoolOutlinedIcon from "@mui/icons-material/SchoolOutlined";
import AccountBalanceOutlinedIcon from "@mui/icons-material/AccountBalanceOutlined";
import AccessTimeOutlinedIcon from "@mui/icons-material/AccessTimeOutlined";
import { useEffect, useMemo, useState } from "react";
import { PermissionKeys } from "../../auth/permissionKeys";
import { useAuth } from "../../context/AuthContext";
import { useTenantContext } from "../../context/TenantContextProvider";
import { getTenantCollege } from "../../services/adminService";
import {
  formatContextRemaining,
  formatContextSelectedLabel,
  formatContextValidUntil,
} from "../../utils/contextFormatUtils";

type Props = {
  onChangeContext?: () => void;
  universityName?: string | null;
};

const OperationalContextBanner = ({ onChangeContext, universityName: universityNameProp }: Props) => {
  const { hasPermission } = useAuth();
  const { context, isSuperAdmin, hasOperationalContext, clearOperationalContext, loading, renewOperationalContext } =
    useTenantContext();
  const [universityName, setUniversityName] = useState<string | null>(universityNameProp ?? null);
  const canReadTenantCollege = hasPermission(PermissionKeys.OrganizationManage);

  useEffect(() => {
    if (universityNameProp) {
      setUniversityName(universityNameProp);
      return;
    }
    // Faculty/attendance users typically lack Organization.Manage — skip admin college profile call
    // (avoids noisy 403/405 and is not required for Mark Attendance).
    if (!hasOperationalContext || !canReadTenantCollege) return;
    void getTenantCollege()
      .then((res) => setUniversityName(res.data.universityName))
      .catch(() => setUniversityName(null));
  }, [hasOperationalContext, universityNameProp, context?.selectedCollegeId, canReadTenantCollege]);

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

  const collegeLabel = context?.selectedCollegeName ?? "College scope";

  return (
    <Paper variant="outlined" sx={{ p: 2 }} role="status" aria-label="Operational context">
      <Stack
        direction={{ xs: "column", lg: "row" }}
        spacing={2}
        sx={{ alignItems: { lg: "center" }, justifyContent: "space-between" }}
      >
        <Box sx={{ flex: 1 }}>
          <Typography variant="overline" color="text.secondary">
            Operational Context
          </Typography>
          <Stack spacing={1.25} sx={{ mt: 0.5 }}>
            <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }}>
              <SchoolOutlinedIcon fontSize="small" color="primary" aria-hidden />
              <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                {collegeLabel}
              </Typography>
              {context?.selectedCollegeCode ? (
                <Chip size="small" label={context.selectedCollegeCode} variant="outlined" />
              ) : null}
            </Stack>
            {universityName ? (
              <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
                <AccountBalanceOutlinedIcon fontSize="small" color="action" aria-hidden />
                <Typography variant="body2" color="text.secondary">
                  {universityName}
                </Typography>
              </Stack>
            ) : null}
            <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", gap: 0.5 }}>
              <Chip size="small" icon={<AccessTimeOutlinedIcon />} label={formatContextSelectedLabel(context?.createdUtc)} variant="outlined" />
              <Chip size="small" label={`Valid until ${formatContextValidUntil(context?.expiresUtc)}`} variant="outlined" />
              <Chip size="small" label={`Expires in ${formatContextRemaining(context?.expiresUtc)}`} color="info" variant="outlined" />
              <Chip size="small" label={`Type: ${contextTypeLabel}`} variant="outlined" />
            </Stack>
          </Stack>
        </Box>
        {isSuperAdmin ? (
          <Stack direction={{ xs: "column", sm: "row" }} spacing={1}>
            <Button size="small" variant="outlined" startIcon={<SwapHorizIcon />} onClick={onChangeContext}>
              Change Context
            </Button>
            <Button size="small" variant="outlined" startIcon={<RefreshOutlinedIcon />} onClick={() => void renewOperationalContext()}>
              Refresh Context
            </Button>
            {!context?.isGlobal ? (
              <Button
                size="small"
                color="inherit"
                startIcon={<CloseIcon />}
                onClick={() => void clearOperationalContext()}
              >
                Clear Context
              </Button>
            ) : null}
          </Stack>
        ) : null}
      </Stack>
    </Paper>
  );
};

export default OperationalContextBanner;
