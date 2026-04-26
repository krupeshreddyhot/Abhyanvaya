import axios from "axios";
import { useEffect, useState, type ChangeEvent } from "react";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  List,
  ListItem,
  ListItemText,
  MenuItem,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import { Link as RouterLink } from "react-router-dom";
import {
  getAdminUniversities,
  getParentCollegeOptions,
  getTenantCollege,
  updateTenantCollege,
  uploadTenantCollegeLogo,
  type ParentCollegeOptionDto,
  type UniversityDto,
} from "../../services/adminService";
import { brandingAssetUrl } from "../../utils/brandingUrl";
import { uploadStudentsExcel, type UploadStudentsResultDto } from "../../services/studentService";

const NO_PARENT_VALUE = "__no_parent__";

/** Tenant admin: edit this college only (catalog). No university creation — use Super Admin Organization for new institutions. */
const CollegeProfilePage = () => {
  const [universities, setUniversities] = useState<UniversityDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [savingCollege, setSavingCollege] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [collegeName, setCollegeName] = useState("");
  const [collegeShortName, setCollegeShortName] = useState("");
  const [collegeCode, setCollegeCode] = useState("");
  const [parentCollegeId, setParentCollegeId] = useState<number | null>(null);
  const [parentCollegeOptions, setParentCollegeOptions] = useState<ParentCollegeOptionDto[]>([]);
  const [parentOptionsLoading, setParentOptionsLoading] = useState(false);
  const [universityId, setUniversityId] = useState<number>(0);

  const [uploadFileName, setUploadFileName] = useState<string | null>(null);
  const [uploadingStudents, setUploadingStudents] = useState(false);
  const [uploadResult, setUploadResult] = useState<UploadStudentsResultDto | null>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);

  const [logoSmPath, setLogoSmPath] = useState<string | null>(null);
  const [logoMdPath, setLogoMdPath] = useState<string | null>(null);
  const [logoLgPath, setLogoLgPath] = useState<string | null>(null);
  const [uploadingLogo, setUploadingLogo] = useState(false);
  const [logoError, setLogoError] = useState<string | null>(null);

  const loadData = async () => {
    setLoading(true);
    setError(null);

    try {
      const [uniRes, collegeRes] = await Promise.all([getAdminUniversities(), getTenantCollege()]);
      setUniversities(uniRes.data);

      const college = collegeRes.data;
      setCollegeName(college.name);
      setCollegeShortName(college.shortName ?? "");
      setCollegeCode(college.code);
      setParentCollegeId(college.parentCollegeId ?? null);
      setUniversityId(college.universityId);
      setLogoSmPath(college.logoSmPath ?? null);
      setLogoMdPath(college.logoMdPath ?? null);
      setLogoLgPath(college.logoLgPath ?? null);
    } catch {
      setError("Failed to load college profile.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadData();
  }, []);

  useEffect(() => {
    if (universityId <= 0) {
      setParentCollegeOptions([]);
      return;
    }

    let cancelled = false;
    setParentOptionsLoading(true);

    void (async () => {
      try {
        const res = await getParentCollegeOptions(universityId);
        if (!cancelled) {
          setParentCollegeOptions(res.data);
        }
      } catch {
        if (!cancelled) {
          setParentCollegeOptions([]);
        }
      } finally {
        if (!cancelled) {
          setParentOptionsLoading(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [universityId]);

  const onSaveCollege = async () => {
    setSavingCollege(true);
    setError(null);
    setMessage(null);

    try {
      await updateTenantCollege({
        name: collegeName.trim(),
        shortName: collegeShortName.trim() || undefined,
        code: collegeCode.trim().toUpperCase(),
        universityId,
        parentCollegeId: parentCollegeId,
      });
      setMessage("College profile updated successfully.");
      await loadData();
    } catch {
      setError("Unable to update college profile.");
    } finally {
      setSavingCollege(false);
    }
  };

  const onPickStudentFile = (e: ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    setUploadError(null);
    setUploadResult(null);
    if (!file) {
      setUploadFileName(null);
      return;
    }
    setUploadFileName(file.name);
    void runStudentUpload(file);
    e.target.value = "";
  };

  const onPickLogo = (e: ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    setLogoError(null);
    e.target.value = "";
    if (!file) return;
    void runLogoUpload(file);
  };

  const toFriendlyLogoError = (message: string | null) => {
    if (!message) return "Unable to upload logo right now. Please try again.";
    const normalized = message.toLowerCase();
    if (normalized.includes("storage upload failed") || normalized.includes("storage health check failed")) {
      return "Logo storage is temporarily unreachable. Please contact support or try again shortly.";
    }
    return message;
  };

  const runLogoUpload = async (file: File) => {
    setUploadingLogo(true);
    setLogoError(null);
    try {
      await uploadTenantCollegeLogo(file);
      window.dispatchEvent(new CustomEvent("abhyanvaya:header-refresh"));
      setMessage("College logo saved (small, medium, large).");
      await loadData();
    } catch (err: unknown) {
      if (axios.isAxiosError(err)) {
        const d = err.response?.data;
        const detail =
          d && typeof d === "object" && "detail" in d && typeof (d as { detail?: unknown }).detail === "string"
            ? (d as { detail: string }).detail
            : null;
        const msg =
          typeof d === "string"
            ? d
            : detail
              ? detail
              : typeof d === "object" && d !== null && "message" in d && typeof (d as { message?: unknown }).message === "string"
                ? (d as { message: string }).message
                : typeof d === "object" && d !== null && "title" in d && typeof (d as { title?: unknown }).title === "string"
                  ? (d as { title: string }).title
                  : null;
        setLogoError(
          toFriendlyLogoError(msg ?? "Unable to upload logo. Use JPEG, PNG, GIF, or WebP under 5 MB.")
        );
      } else {
        setLogoError("Unable to upload logo right now. Please try again.");
      }
    } finally {
      setUploadingLogo(false);
    }
  };

  const runStudentUpload = async (file: File) => {
    setUploadingStudents(true);
    setUploadError(null);
    setUploadResult(null);
    try {
      const res = await uploadStudentsExcel(file);
      setUploadResult(res.data);
    } catch (err: unknown) {
      if (axios.isAxiosError(err)) {
        const d = err.response?.data;
        setUploadError(typeof d === "string" ? d : err.message || "Upload failed");
      } else {
        setUploadError("Upload failed");
      }
    } finally {
      setUploadingStudents(false);
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
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup" startIcon={<ArrowBackIcon />} variant="text">
          Catalog
        </Button>
        <Typography variant="h4" sx={{ flexGrow: 1 }}>
          College profile
        </Typography>
      </Box>

      <Typography variant="body2" color="text.secondary">
        Edit your institution&apos;s college record, branding, and import students. To create a new university or a
        new college tenant, a Super Admin must use Organization.
      </Typography>

      {message && <Alert severity="success">{message}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}

      <Card>
        <CardContent>
          <Typography variant="h6" sx={{ mb: 2 }}>
            College details &amp; logo
          </Typography>

          <Stack spacing={2}>
            <TextField label="College Name" value={collegeName} onChange={(e) => setCollegeName(e.target.value)} fullWidth />

            <TextField
              label="Short Name (for mobile header)"
              value={collegeShortName}
              onChange={(e) => setCollegeShortName(e.target.value)}
              fullWidth
            />

            <Box>
              <Typography variant="subtitle2" sx={{ mb: 0.5 }}>
                College logo
              </Typography>
              <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
                Upload one image (JPEG, PNG, GIF, or WebP, max 5 MB). The server saves three WebP sizes.
              </Typography>
              <Box sx={{ display: "flex", flexWrap: "wrap", gap: 1, alignItems: "center", mb: 1 }}>
                <Button variant="outlined" component="label" disabled={uploadingLogo}>
                  {uploadingLogo ? (
                    <>
                      <CircularProgress size={18} sx={{ mr: 1 }} /> Processing…
                    </>
                  ) : (
                    "Upload logo"
                  )}
                  <input type="file" accept="image/jpeg,image/png,image/gif,image/webp" hidden onChange={onPickLogo} />
                </Button>
              </Box>
              {logoError && (
                <Alert severity="error" sx={{ mb: 1 }}>
                  {logoError}
                </Alert>
              )}
              {(logoSmPath || logoMdPath || logoLgPath) && (
                <Table size="small" sx={{ border: 1, borderColor: "divider", borderRadius: 1 }}>
                  <TableHead>
                    <TableRow>
                      <TableCell>Variant</TableCell>
                      <TableCell align="center">Preview</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {[
                      { label: "Small", path: logoSmPath },
                      { label: "Medium", path: logoMdPath },
                      { label: "Large", path: logoLgPath },
                    ].map((row) => (
                      <TableRow key={row.label}>
                        <TableCell>{row.label}</TableCell>
                        <TableCell align="center">
                          {row.path ? (
                            <Box
                              component="img"
                              src={brandingAssetUrl(row.path) ?? undefined}
                              alt=""
                              sx={{ maxHeight: 72, maxWidth: "100%", objectFit: "contain", verticalAlign: "middle" }}
                            />
                          ) : (
                            "—"
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </Box>

            <TextField
              label="College Code"
              value={collegeCode}
              onChange={(e) => setCollegeCode(e.target.value.toUpperCase())}
              fullWidth
            />

            <TextField
              select
              label="University"
              value={universityId}
              onChange={(e) => {
                const next = Number(e.target.value);
                setUniversityId(next);
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

            <TextField
              select
              label="Parent college (optional)"
              value={parentCollegeId === null ? NO_PARENT_VALUE : String(parentCollegeId)}
              onChange={(e) => {
                const raw = e.target.value;
                setParentCollegeId(raw === NO_PARENT_VALUE ? null : Number.parseInt(raw, 10));
              }}
              fullWidth
              disabled={parentOptionsLoading || universityId <= 0}
              helperText="Main campus or hub under the same university."
              slotProps={{
                inputLabel: { shrink: true },
                select: {
                  renderValue: (value: unknown) => {
                    const str = String(value ?? "");
                    if (str === NO_PARENT_VALUE) {
                      return "None (standalone / main campus)";
                    }
                    const id = Number.parseInt(str, 10);
                    const c = parentCollegeOptions.find((o) => o.id === id);
                    if (c) {
                      return `${c.name} (${c.code})${c.shortName ? ` — ${c.shortName}` : ""}`;
                    }
                    return str;
                  },
                },
              }}
            >
              <MenuItem value={NO_PARENT_VALUE}>None (standalone / main campus)</MenuItem>
              {parentCollegeOptions.map((c) => (
                <MenuItem key={c.id} value={String(c.id)}>
                  {c.name} ({c.code})
                  {c.shortName ? ` — ${c.shortName}` : ""}
                </MenuItem>
              ))}
            </TextField>

            <Button variant="contained" onClick={() => void onSaveCollege()} disabled={savingCollege}>
              {savingCollege ? "Saving..." : "Save college profile"}
            </Button>
          </Stack>
        </CardContent>
      </Card>

      <Card>
        <CardContent>
          <Typography variant="h6" sx={{ mb: 1 }}>
            Upload students (Excel)
          </Typography>

          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            Use the first sheet with headers. Required: StudentNumber, Name, CourseId, GroupId, GenderId, MediumId,
            LanguageId, SemesterId. Optional: college_code (must match this tenant), and other fields as documented in
            Organization import.
          </Typography>

          <Box sx={{ display: "flex", flexWrap: "wrap", gap: 2, alignItems: "center", mb: 2 }}>
            <Button variant="contained" component="label" disabled={uploadingStudents}>
              {uploadingStudents ? "Uploading…" : "Choose Excel file"}
              <input
                type="file"
                accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                hidden
                onChange={onPickStudentFile}
              />
            </Button>
            {uploadFileName && (
              <Typography variant="body2" color="text.secondary">
                Last file: {uploadFileName}
              </Typography>
            )}
          </Box>

          {uploadError && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {uploadError}
            </Alert>
          )}

          {uploadResult && (
            <Box>
              <Alert severity={uploadResult.imported > 0 ? "success" : "info"} sx={{ mb: 1 }}>
                Imported: {uploadResult.imported}. Skipped: {uploadResult.skipped}.
              </Alert>
              {uploadResult.errors.length > 0 && (
                <Box sx={{ maxHeight: 280, overflow: "auto", border: 1, borderColor: "divider", borderRadius: 1, p: 1 }}>
                  <Typography variant="subtitle2" sx={{ mb: 1 }}>
                    Row messages ({uploadResult.errors.length})
                  </Typography>
                  <List dense disablePadding>
                    {uploadResult.errors.map((line, i) => (
                      <ListItem key={i} disableGutters sx={{ py: 0.25 }}>
                        <ListItemText primary={line} slotProps={{ primary: { sx: { typography: "body2" } } }} />
                      </ListItem>
                    ))}
                  </List>
                </Box>
              )}
            </Box>
          )}
        </CardContent>
      </Card>
    </Stack>
  );
};

export default CollegeProfilePage;
