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
import {
  createUniversity,
  getAdminUniversities,
  getParentCollegeOptions,
  getTenantCollege,
  updateTenantCollege,
  uploadTenantCollegeLogo,
  type ParentCollegeOptionDto,
  type UniversityDto,
} from "../services/adminService";
import { brandingAssetUrl } from "../utils/brandingUrl";
import { uploadStudentsExcel, type UploadStudentsResultDto } from "../services/studentService";

/** Sentinel for MUI Select — empty string causes label/value overlap with outlined TextField. */
const NO_PARENT_VALUE = "__no_parent__";

const AdminSetup = () => {
  const [universities, setUniversities] = useState<UniversityDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [savingCollege, setSavingCollege] = useState(false);
  const [savingUniversity, setSavingUniversity] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [collegeName, setCollegeName] = useState("");
  const [collegeCode, setCollegeCode] = useState("");
  const [collegeShortName, setCollegeShortName] = useState("");
  const [parentCollegeId, setParentCollegeId] = useState<number | null>(null);
  const [parentCollegeOptions, setParentCollegeOptions] = useState<ParentCollegeOptionDto[]>([]);
  const [parentOptionsLoading, setParentOptionsLoading] = useState(false);
  const [universityId, setUniversityId] = useState<number>(0);

  const [newUniversityCode, setNewUniversityCode] = useState("");
  const [newUniversityName, setNewUniversityName] = useState("");

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
      setError("Failed to load admin setup data.");
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
      setMessage("University created successfully.");
      await loadData();
    } catch {
      setError("Unable to create university. Code may already exist.");
    } finally {
      setSavingUniversity(false);
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

  const runLogoUpload = async (file: File) => {
    setUploadingLogo(true);
    setLogoError(null);
    try {
      await uploadTenantCollegeLogo(file);
      window.dispatchEvent(new CustomEvent("abhyanvaya:header-refresh"));
      setMessage("College logo saved (small, medium, large).");
      await loadData();
    } catch {
      setLogoError("Unable to upload logo. Use JPEG, PNG, GIF, or WebP under 5 MB.");
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
    return <Typography>Loading setup...</Typography>;
  }

  return (
    <Stack spacing={3}>
      <Typography variant="h4">Tenant Setup</Typography>

      {message && <Alert severity="success">{message}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "2fr 1fr" }, gap: 3 }}>
        <Card>
          <CardContent>
            <Typography variant="h6" sx={{ mb: 2 }}>
              College Profile
            </Typography>

            <Stack spacing={2}>
              <TextField
                label="College Name"
                value={collegeName}
                onChange={(e) => setCollegeName(e.target.value)}
                fullWidth
              />

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
                  Upload one image (JPEG, PNG, GIF, or WebP, max 5 MB). The server saves three WebP sizes for phones,
                  tablets, and desktops (max edge 64 / 128 / 256 px).
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
                helperText="Main campus or hub under the same university. Leave unset if this college is not a branch."
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

              <Button variant="contained" onClick={onSaveCollege} disabled={savingCollege}>
                {savingCollege ? "Saving..." : "Save College Profile"}
              </Button>
            </Stack>
          </CardContent>
        </Card>

        <Card>
          <CardContent>
            <Typography variant="h6" sx={{ mb: 2 }}>
              Add University
            </Typography>

            <Stack spacing={2}>
              <TextField
                label="University Code"
                value={newUniversityCode}
                onChange={(e) => setNewUniversityCode(e.target.value.toUpperCase())}
                fullWidth
              />

              <TextField
                label="University Name"
                value={newUniversityName}
                onChange={(e) => setNewUniversityName(e.target.value)}
                fullWidth
              />

              <Button variant="outlined" onClick={onCreateUniversity} disabled={savingUniversity}>
                {savingUniversity ? "Creating..." : "Create University"}
              </Button>
            </Stack>
          </CardContent>
        </Card>
      </Box>

      <Card>
        <CardContent>
          <Typography variant="h6" sx={{ mb: 1 }}>
            Upload students (Excel)
          </Typography>

          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            Use the first sheet. Row 1 must be headers. Required columns: <strong>StudentNumber</strong>,{" "}
            <strong>Name</strong>, <strong>CourseId</strong>, <strong>GorupID</strong> (or GroupId),{" "}
            <strong>GenderId</strong>, <strong>MediumId</strong>, <strong>LanguageId</strong>,{" "}
            <strong>SemesterId</strong>. Optional: college_code (must match this tenant), AppraId, Batch, DateOfBirth
            (DD-MM-YYYY), MobileNumber, AlternateMobileNumber, Email, parent phones, FatherName, MotherName. Format{" "}
            <strong>StudentNumber</strong> as Text in Excel if values are long, to avoid rounding. Empty rows are
            skipped.
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
                        <ListItemText
                          primary={line}
                          slotProps={{ primary: { sx: { typography: "body2" } } }}
                        />
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

export default AdminSetup;
