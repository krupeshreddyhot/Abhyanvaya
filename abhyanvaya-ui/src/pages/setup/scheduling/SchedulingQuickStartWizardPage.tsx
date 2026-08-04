import { useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Stack,
  Step,
  StepLabel,
  Stepper,
  Typography,
} from "@mui/material";
import { Link as RouterLink, useNavigate } from "react-router-dom";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";

type WizardStep = {
  title: string;
  path: string;
  body: string;
};

/** AI30.3.5.3 — Quick Start Wizard. Steps loaded from markdown; no DB/workflow changes. */
const SchedulingQuickStartWizardPage = () => {
  const navigate = useNavigate();
  const [md, setMd] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [active, setActive] = useState(0);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    void (async () => {
      try {
        const res = await fetch("/docs/scheduling/quick-start.md");
        if (!res.ok) throw new Error("Unable to load quick-start.md");
        setMd(await res.text());
      } catch (e) {
        setError(e instanceof Error ? e.message : "Failed to load wizard.");
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  const steps = useMemo(() => parseSteps(md), [md]);

  if (loading) return <CircularProgress sx={{ m: 2 }} />;

  const step = steps[active];

  return (
    <Stack spacing={2} sx={{ p: { xs: 1.5, md: 2 } }}>
      <Box sx={{ display: "flex", gap: 1, alignItems: "center", flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />}>
          Scheduling
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Quick Start Guide
        </Typography>
      </Box>
      {error && <Alert severity="error">{error}</Alert>}
      <Typography variant="body2" color="text.secondary">
        Minimum configuration before timetable creation. No data is changed by this wizard.
      </Typography>

      <Stepper activeStep={active} alternativeLabel sx={{ display: { xs: "none", md: "flex" } }}>
        {steps.map((s) => (
          <Step key={s.title}>
            <StepLabel>{s.title}</StepLabel>
          </Step>
        ))}
      </Stepper>
      <Typography variant="caption" sx={{ display: { xs: "block", md: "none" } }}>
        Step {active + 1} of {steps.length}: {step?.title}
      </Typography>

      {step && (
        <Box sx={{ p: 2, border: 1, borderColor: "divider", borderRadius: 1 }}>
          <Typography variant="h6" sx={{ mb: 1 }}>
            {step.title}
          </Typography>
          <Typography variant="body1" color="text.secondary" sx={{ whiteSpace: "pre-wrap", mb: 2 }}>
            {step.body}
          </Typography>
          <Button component={RouterLink} to={step.path} variant="outlined" size="small">
            Open {step.title}
          </Button>
        </Box>
      )}

      <Stack direction="row" spacing={1}>
        <Button disabled={active === 0} onClick={() => setActive((x) => x - 1)}>
          Previous
        </Button>
        {active < steps.length - 1 ? (
          <Button variant="contained" onClick={() => setActive((x) => x + 1)}>
            Next
          </Button>
        ) : (
          <Button variant="contained" onClick={() => navigate("/setup/scheduling")}>
            Finish
          </Button>
        )}
      </Stack>
    </Stack>
  );
};

function parseSteps(md: string): WizardStep[] {
  if (!md.trim()) {
    return [
      {
        title: "Academic Year",
        path: "/setup/scheduling/academic-years",
        body: "Create and mark the current academic year.",
      },
    ];
  }
  const chunks = md.split(/\n## /).slice(1);
  return chunks.map((chunk) => {
    const lines = chunk.split("\n");
    const title = lines[0]?.trim() ?? "Step";
    const pathLine = lines.find((l) => l.startsWith("path:"));
    const path = pathLine?.replace("path:", "").trim() || "/setup/scheduling";
    const body = lines
      .slice(1)
      .filter((l) => !l.startsWith("path:"))
      .join("\n")
      .trim();
    return { title, path, body };
  });
}

export default SchedulingQuickStartWizardPage;
