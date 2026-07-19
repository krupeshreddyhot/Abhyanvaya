import { Alert, Chip, Stack, Typography } from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import { useTenantContext } from "../../context/TenantContextProvider";

const ContextBanner = () => {
  const { context, isSuperAdmin, needsCollegeSelection, clearOperationalContext, loading } = useTenantContext();

  if (loading) {
    return null;
  }

  if (needsCollegeSelection) {
    return (
      <Alert severity="warning" role="status">
        No college context selected. Choose a college to load AI and tenant-scoped data.
      </Alert>
    );
  }

  if (!context?.selectedCollegeId && !isSuperAdmin) {
    return null;
  }

  const label = context?.selectedCollegeName
    ? `${context.selectedCollegeName} (${context.selectedCollegeCode ?? context.selectedCollegeId})`
    : context?.isGlobal
      ? "Global context"
      : "College context";

  return (
    <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }} role="status">
      <Typography variant="caption" color="text.secondary">
        Current context
      </Typography>
      <Chip size="small" label={label} color={context?.isGlobal ? "default" : "primary"} variant="outlined" />
      {isSuperAdmin && !context?.isGlobal ? (
        <Chip
          size="small"
          label="Clear context"
          onClick={() => void clearOperationalContext()}
          onDelete={() => void clearOperationalContext()}
          deleteIcon={<CloseIcon />}
          variant="outlined"
        />
      ) : null}
    </Stack>
  );
};

export default ContextBanner;
