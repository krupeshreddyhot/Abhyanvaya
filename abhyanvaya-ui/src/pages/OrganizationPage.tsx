import axios from "axios";
import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Link,
  MenuItem,
  Stack,
  TextField,
  Typography,
  CircularProgress,
} from "@mui/material";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import {
  createUniversity,
  getAdminUniversities,
  getOrganizationParentCollegeOptions,
  provisionCollege,
  type UniversityDto,
} from "../services/adminService";

const NO_PARENT_VALUE = "__no_parent__";

/** Super Admin only: create universities and provision new college tenants. */
const OrganizationPage = () => {
  const [universities, setUniversities] = useState<UniversityDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [newUniversityCode, setNewUniversityCode] = useState("");
  const [newUniversityName, setNewUniversityName] = useState("");
  const [savingUniversity, setSavingUniversity] = useState(false);

  const [provisionUniversityId, setProvisionUniversityId] = useState(0);
  const [collegeName, setCollegeName] = useState("");
  const [collegeCode, setCollegeCode] = useState("");
  const [parentCollegeId, setParentCollegeId] = useState<number | null>(null);
  const [parentOptions, setParentOptions] = useState<{ id: number; name: string; code: string; shortName?: string | null }[]>(
    [],
  );
  const [adminUsername, setAdminUsername] = useState("");
  const [adminPassword, setAdminPassword] = useState("");
  const [provisioning, setProvisioning] = useState(false);

  const loadUniversities = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await getAdminUniversities();
      setUniversities(res.data);
      if (res.data.length > 0 && provisionUniversityId <= 0) {
        setProvisionUniversityId(res.data[0].id);
      }
    } catch {
      setError("Failed to load universities.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadUniversities();
  }, []);

  useEffect(() => {
    if (provisionUniversityId <= 0) {
      setParentOptions([]);
      return;
    }
    let cancelled = false;
    void (async () => {
      try {
        const res = await getOrganizationParentCollegeOptions(provisionUniversityId);
        if (!cancelled) {
          setParentOptions(res.data);
        }
      } catch {
        if (!cancelled) setParentOptions([]);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [provisionUniversityId]);

  const onCreateUniversity = async () => {
    setSavingUniversity(true);
    setError(null);
    setMessage(null);
    try {
      await createUniversity({
        code: newUniversityCode.trim().toUpperCase(),
        name: newUniversityName.trim(),
      });
      setNewUniversityCode("");
      setNewUniversityName("");
      setMessage("University created.");
      await loadUniversities();
    } catch {
      setError("Unable to create university. Code may already exist.");
    } finally {
      setSavingUniversity(false);
    }
  };

  const onProvision = async () => {
    setProvisioning(true);
    setError(null);
    setMessage(null);
    try {
      const res = await provisionCollege({
        universityId: provisionUniversityId,
        collegeName: collegeName.trim(),
        collegeCode: collegeCode.trim(),
        parentCollegeId: parentCollegeId ?? undefined,
        adminUsername: adminUsername.trim(),
        adminPassword: adminPassword,
      });
      const row = res.data as Record<string, unknown>;
      const tenantId = Number(row.tenantId ?? row.TenantId ?? 0);
      const uniCode = String(row.universityCode ?? row.UniversityCode ?? "");
      const colCode = String(row.collegeCode ?? row.CollegeCode ?? "");
      setMessage(
        `College provisioned. Tenant ${tenantId}. First institution admin signs in under Institution login using university code ${uniCode}, college code ${colCode}, and the username/password you set.`,
      );
      setCollegeName("");
      setCollegeCode("");
      setAdminPassword("");
      await loadUniversities();
    } catch (e: unknown) {
      if (axios.isAxiosError(e)) {
        const d = e.response?.data;
        setError(typeof d === "string" ? d : "Could not provision college.");
      } else {
        setError("Could not provision college.");
      }
    } finally {
      setProvisioning(false);
    }
  };

  if (loading) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Stack spacing={3}>
      <Typography variant="h4">Organization</Typography>
      <Box sx={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 1.5 }}>
        <Typography variant="body2" color="text.secondary" component="span">
          Documentation
        </Typography>
        <Link href="/docs/index.html" target="_blank" rel="noopener noreferrer" variant="body2" sx={{ display: "inline-flex", alignItems: "center", gap: 0.5 }}>
          Overview &amp; CSV downloads
          <OpenInNewIcon sx={{ fontSize: "1rem" }} aria-hidden />
        </Link>
        <Link
          href="/docs/authorization-matrix.html"
          target="_blank"
          rel="noopener noreferrer"
          variant="body2"
          sx={{ display: "inline-flex", alignItems: "center", gap: 0.5 }}
        >
          Full authorization matrix (HTML)
          <OpenInNewIcon sx={{ fontSize: "1rem" }} aria-hidden />
        </Link>
      </Box>
      <Typography variant="body2" color="text.secondary">
        Super Admin: create universities and provision new colleges (each college is a separate tenant with its own
        admin login).
      </Typography>

      {message && <Alert severity="success">{message}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "1fr 1fr" }, gap: 3 }}>
        <Card variant="outlined">
          <CardContent>
            <Typography variant="h6" sx={{ mb: 2 }}>
              Add university
            </Typography>
            <Stack spacing={2}>
              <TextField
                label="University code"
                value={newUniversityCode}
                onChange={(e) => setNewUniversityCode(e.target.value.toUpperCase())}
                fullWidth
              />
              <TextField label="University name" value={newUniversityName} onChange={(e) => setNewUniversityName(e.target.value)} fullWidth />
              <Button variant="outlined" onClick={() => void onCreateUniversity()} disabled={savingUniversity}>
                {savingUniversity ? "Creating…" : "Create university"}
              </Button>
            </Stack>
          </CardContent>
        </Card>

        <Card variant="outlined">
          <CardContent>
            <Typography variant="h6" sx={{ mb: 2 }}>
              Provision new college (new tenant)
            </Typography>
            <Stack spacing={2}>
              <TextField
                select
                label="University"
                value={provisionUniversityId}
                onChange={(e) => {
                  setProvisionUniversityId(Number(e.target.value));
                  setParentCollegeId(null);
                }}
                fullWidth
              >
                {universities.map((u) => (
                  <MenuItem key={u.id} value={u.id}>
                    {u.name} ({u.code})
                  </MenuItem>
                ))}
              </TextField>
              <TextField label="College name" value={collegeName} onChange={(e) => setCollegeName(e.target.value)} fullWidth />
              <TextField
                label="College code"
                value={collegeCode}
                onChange={(e) => setCollegeCode(e.target.value.toUpperCase())}
                fullWidth
              />
              <TextField
                select
                label="Parent college (optional)"
                value={parentCollegeId === null ? NO_PARENT_VALUE : String(parentCollegeId)}
                onChange={(e) => {
                  const raw = e.target.value;
                  setParentCollegeId(raw === NO_PARENT_VALUE ? null : Number.parseInt(raw, 10));
                }}
                fullWidth
                disabled={provisionUniversityId <= 0}
              >
                <MenuItem value={NO_PARENT_VALUE}>None</MenuItem>
                {parentOptions.map((c) => (
                  <MenuItem key={c.id} value={String(c.id)}>
                    {c.name} ({c.code})
                  </MenuItem>
                ))}
              </TextField>
              <TextField label="First admin username" value={adminUsername} onChange={(e) => setAdminUsername(e.target.value)} fullWidth />
              <TextField
                label="First admin password"
                type="password"
                value={adminPassword}
                onChange={(e) => setAdminPassword(e.target.value)}
                fullWidth
              />
              <Button variant="contained" onClick={() => void onProvision()} disabled={provisioning}>
                {provisioning ? "Provisioning…" : "Provision college"}
              </Button>
            </Stack>
          </CardContent>
        </Card>
      </Box>
    </Stack>
  );
};

export default OrganizationPage;
