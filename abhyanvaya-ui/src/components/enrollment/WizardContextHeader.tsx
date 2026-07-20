import { Alert, Chip, Paper, Stack, Typography } from "@mui/material";
import SchoolOutlinedIcon from "@mui/icons-material/SchoolOutlined";
import AccountBalanceOutlinedIcon from "@mui/icons-material/AccountBalanceOutlined";
import HubOutlinedIcon from "@mui/icons-material/HubOutlined";
import { formatContextSelectedLabel } from "../../utils/contextFormatUtils";

type Props = {
  collegeName: string;
  collegeCode?: string | null;
  universityName: string;
  contextCreatedUtc?: string | null;
};

const WizardContextHeader = ({
  collegeName,
  collegeCode,
  universityName,
  contextCreatedUtc,
}: Props) => (
  <Paper
    variant="outlined"
    sx={{ p: 2, mb: 2, bgcolor: "action.hover" }}
    role="region"
    aria-label="Operational context summary"
  >
    <Typography variant="overline" color="text.secondary" sx={{ display: "block", mb: 1 }}>
      Operational Context
    </Typography>
    <Stack spacing={1.5}>
      <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }}>
        <SchoolOutlinedIcon fontSize="small" color="primary" aria-hidden />
        <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
          {collegeName}
        </Typography>
        {collegeCode ? (
          <Chip size="small" label={collegeCode} variant="outlined" aria-label={`College code ${collegeCode}`} />
        ) : null}
      </Stack>
      <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
        <AccountBalanceOutlinedIcon fontSize="small" color="action" aria-hidden />
        <Typography variant="body2" color="text.secondary">
          {universityName}
        </Typography>
      </Stack>
      <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
        <HubOutlinedIcon fontSize="small" color="action" aria-hidden />
        <Typography variant="caption" color="text.secondary">
          {formatContextSelectedLabel(contextCreatedUtc)}
        </Typography>
      </Stack>
    </Stack>
    <Alert severity="info" sx={{ mt: 2 }} icon={false}>
      College is taken from your operational context. You will not be asked to select it again.
    </Alert>
  </Paper>
);

export default WizardContextHeader;
