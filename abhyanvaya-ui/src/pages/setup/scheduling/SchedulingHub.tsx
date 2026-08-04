import { useEffect, useMemo, useState } from "react";
import { Link as RouterLink } from "react-router-dom";
import {
  Alert,
  Box,
  Button,
  Card,
  CardActionArea,
  CardContent,
  Chip,
  CircularProgress,
  IconButton,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import MenuBookIcon from "@mui/icons-material/MenuBook";
import RocketLaunchIcon from "@mui/icons-material/RocketLaunch";
import NavigateNextIcon from "@mui/icons-material/NavigateNext";
import {
  getSchedulingConfigurationReadiness,
  type SchedulingModuleStatus,
  type SchedulingReadinessSummary,
} from "../../../services/schedulingService";
import ModuleHelpDrawer from "./ModuleHelpDrawer";
import { schedulingDashboardLink, schedulingHubGroups } from "./schedulingCatalogConfig";

const statusColor = (status?: string) => {
  switch (status) {
    case "Complete":
      return "success";
    case "Partial":
      return "info";
    case "Required":
    case "Missing":
      return "warning";
    case "Blocked":
      return "error";
    case "Optional":
    default:
      return "default";
  }
};

const SchedulingHub = () => {
  const [readiness, setReadiness] = useState<SchedulingReadinessSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [helpModule, setHelpModule] = useState<SchedulingModuleStatus | null>(null);

  useEffect(() => {
    void (async () => {
      try {
        const res = await getSchedulingConfigurationReadiness();
        setReadiness(res.data);
      } catch {
        setReadiness(null);
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  const byKey = useMemo(() => {
    const map = new Map<string, SchedulingModuleStatus>();
    readiness?.modules.forEach((m) => map.set(m.moduleKey, m));
    return map;
  }, [readiness]);

  return (
    <Stack spacing={3}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup" startIcon={<ArrowBackIcon />} variant="text">
          Catalog
        </Button>
        <Typography variant="h4" sx={{ flexGrow: 1 }}>
          Scheduling
        </Typography>
        <Button
          component={RouterLink}
          to="/setup/scheduling/configuration-guide"
          startIcon={<MenuBookIcon />}
          variant="outlined"
          size="small"
        >
          Configuration Guide
        </Button>
        <Button
          component={RouterLink}
          to="/setup/scheduling/quick-start"
          startIcon={<RocketLaunchIcon />}
          variant="contained"
          size="small"
        >
          Quick Start
        </Button>
      </Box>

      <Typography variant="body1" color="text.secondary">
        Enterprise scheduling catalog ordered by configuration dependencies. Departments remain under Catalog →
        Departments (single source of truth). Attendance APIs and AttendanceSessionResolver are unchanged.
      </Typography>

      {loading && <CircularProgress size={28} />}

      {readiness && (
        <Alert
          severity={readiness.overallPercent >= 80 ? "success" : "info"}
          action={
            readiness.nextRecommendedStep ? (
              <Button
                color="inherit"
                size="small"
                component={RouterLink}
                to={readiness.nextRecommendedStep.path}
                endIcon={<NavigateNextIcon />}
              >
                Next: {readiness.nextRecommendedStep.title}
              </Button>
            ) : undefined
          }
        >
          Configuration progress {readiness.overallPercent.toFixed(0)}% · Complete {readiness.completedModules} ·
          Pending {readiness.pendingModules} · Blocked {readiness.blockedModules}
          {readiness.nextRecommendedStep ? ` · Recommended: ${readiness.nextRecommendedStep.title}` : ""}
        </Alert>
      )}

      <Card variant="outlined">
        <CardActionArea component={RouterLink} to={schedulingDashboardLink.to}>
          <CardContent>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
              {schedulingDashboardLink.icon}
              <Typography variant="h6">{schedulingDashboardLink.title}</Typography>
            </Box>
            <Typography variant="body2" color="text.secondary">
              {schedulingDashboardLink.description}
            </Typography>
          </CardContent>
        </CardActionArea>
      </Card>

      {schedulingHubGroups.map((group) => (
        <Box key={group.id}>
          <Typography variant="h6" sx={{ mb: 1.5 }}>
            {group.title}
          </Typography>
          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", md: "repeat(3, 1fr)" },
              gap: 2,
            }}
          >
            {group.items.map((x) => {
              const status = byKey.get(x.key);
              return (
                <Card key={x.to} variant="outlined">
                  <CardActionArea component={RouterLink} to={x.to}>
                    <CardContent>
                      <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 0.5 }}>
                        {x.icon}
                        <Typography variant="h6" sx={{ flexGrow: 1, fontSize: "1.05rem" }}>
                          {x.title}
                        </Typography>
                        {status && (
                          <Tooltip title={status.tooltip}>
                            <Chip size="small" label={status.status} color={statusColor(status.status) as "default"} />
                          </Tooltip>
                        )}
                      </Box>
                      <Typography variant="body2" color="text.secondary">
                        {x.description}
                      </Typography>
                    </CardContent>
                  </CardActionArea>
                  <Box sx={{ px: 1, pb: 1, display: "flex", justifyContent: "flex-end" }}>
                    <Tooltip title="Help & dependencies">
                      <IconButton
                        size="small"
                        aria-label={`Help for ${x.title}`}
                        onClick={(e) => {
                          e.preventDefault();
                          setHelpModule(
                            status ?? {
                              moduleKey: x.key,
                              path: x.to,
                              title: x.title,
                              status: "Optional",
                              tooltip: x.description,
                              requires: [],
                              usedBy: [],
                              relatedModules: [],
                              helpDocPath: x.helpDocPath,
                            },
                          );
                        }}
                      >
                        <InfoOutlinedIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </Box>
                </Card>
              );
            })}
          </Box>
        </Box>
      ))}

      <ModuleHelpDrawer open={!!helpModule} onClose={() => setHelpModule(null)} module={helpModule} />
    </Stack>
  );
};

export default SchedulingHub;
