import { useEffect, useState } from "react";
import { Alert, Box, Chip, CircularProgress, Paper, Stack, Typography } from "@mui/material";
import { getEnterpriseHealth, type EnterpriseHealthCenterDto } from "../../services/enterpriseDashboardService";

const color = (status: string) =>
  status === "Red" ? "error" : status === "Yellow" ? "warning" : "success";

/** AI31.6.10 — read-only Enterprise Health Center. */
const EnterpriseHealthCenterPage = () => {
  const [data, setData] = useState<EnterpriseHealthCenterDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const load = async () => {
      try {
        const res = await getEnterpriseHealth();
        setData(res.data);
      } catch {
        setError("Unable to load Health Center.");
      } finally {
        setLoading(false);
      }
    };
    void load();
  }, []);

  if (loading) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", p: 4 }}>
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box>
      <Stack direction="row" spacing={2} sx={{ alignItems: "center", mb: 2 }}>
        <Typography variant="h4">Enterprise Health Center</Typography>
        {data && <Chip label={data.overallStatus} color={color(data.overallStatus)} />}
      </Stack>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Read-only traffic lights reused from existing health monitors.
      </Typography>
      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr", md: "1fr 1fr 1fr" },
          gap: 2,
        }}
      >
        {(data?.components ?? []).map((c) => (
          <Paper key={c.code} sx={{ p: 2 }}>
            <Stack direction="row" spacing={1} sx={{ justifyContent: "space-between", mb: 1 }}>
              <Typography sx={{ fontWeight: 600 }}>{c.title}</Typography>
              <Chip size="small" label={c.status} color={color(c.status)} />
            </Stack>
            <Typography variant="body2" color="text.secondary">
              {c.message}
            </Typography>
          </Paper>
        ))}
      </Box>
    </Box>
  );
};

export default EnterpriseHealthCenterPage;
