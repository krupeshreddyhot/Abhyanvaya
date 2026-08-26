import { useEffect, useMemo, type ReactNode } from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  FormHelperText,
  InputAdornment,
  MenuItem,
  Stack,
  TextField,
  Typography,
  type SxProps,
  type Theme,
} from "@mui/material";
import { useAcademicUi } from "../../context/AcademicUiContext";
import type { AcademicUiSelection } from "../../types/academicUiContext";
import {
  DEFAULT_ACADEMIC_SELECTOR_FIELDS,
  resolveAcademicSelectorFieldState,
  type AcademicSelectorField,
} from "../../utils/academicSelectorFieldState";

export type AcademicScopeSelectorProps = {
  /** Fields to render (Program auto-hidden when tenant Programs disabled). */
  fields?: AcademicSelectorField[];
  disabled?: boolean;
  size?: "small" | "medium";
  /** Show cascade path hint under the row. */
  showCascadeHint?: boolean;
  /** Show context-level error Alert. Default true. */
  showError?: boolean;
  /** Allow clearing Section (optional operational grouping). Default true. */
  sectionOptional?: boolean;
  /** Allow clearing Subject. Default false. */
  subjectOptional?: boolean;
  /** Allow empty Group (e.g. filter "All"). Default false. */
  groupOptional?: boolean;
  /** Allow empty Semester. Default false. */
  semesterOptional?: boolean;
  /** Prefer current academic year when none selected. Default true. */
  autoSelectCurrentYear?: boolean;
  onSelectionChange?: (selection: AcademicUiSelection) => void;
  sx?: SxProps<Theme>;
};

const FIELD_LABEL: Record<AcademicSelectorField, string> = {
  academicYear: "Academic Year",
  program: "Program",
  course: "Course",
  group: "Group",
  semester: "Semester",
  section: "Section",
  subject: "Subject",
};

const FIELD_MIN_WIDTH: Record<AcademicSelectorField, number> = {
  academicYear: 180,
  program: 180,
  course: 160,
  group: 140,
  semester: 140,
  section: 160,
  subject: 180,
};

/**
 * Reusable cascading academic scope selector for AI29.1D.
 * Consumes AcademicUiContext — do not re-filter catalogs in page components.
 *
 * Cascade: Academic Year → Program? → Course → Group → Semester → Section? → Subject
 * Subject is filtered by Course + Group + Semester only (Section is not Subject Master).
 */
const AcademicScopeSelector = ({
  fields = DEFAULT_ACADEMIC_SELECTOR_FIELDS,
  disabled = false,
  size = "small",
  showCascadeHint = false,
  showError = true,
  sectionOptional = true,
  subjectOptional = false,
  groupOptional = false,
  semesterOptional = false,
  autoSelectCurrentYear = true,
  onSelectionChange,
  sx,
}: AcademicScopeSelectorProps) => {
  const {
    enablePrograms,
    programsAvailable,
    hierarchyFailed,
    loading,
    sectionsLoading,
    subjectsLoading,
    error,
    selection,
    catalogs,
    options,
    cascadePath,
    setSelection,
    refreshCatalogs,
  } = useAcademicUi();

  useEffect(() => {
    onSelectionChange?.(selection);
  }, [selection, onSelectionChange]);

  useEffect(() => {
    if (!autoSelectCurrentYear || selection.academicYearId != null || catalogs.academicYears.length === 0) return;
    const current = catalogs.academicYears.find((y) => y.isCurrent) ?? catalogs.academicYears[0];
    if (current) setSelection({ academicYearId: current.id });
  }, [autoSelectCurrentYear, catalogs.academicYears, selection.academicYearId, setSelection]);

  const optionCountByField = useMemo(
    () =>
      ({
        academicYear: catalogs.academicYears.length,
        program: options.programs.length,
        course: options.courses.length,
        group: options.groups.length,
        semester: options.semesters.length,
        section: options.sections.length,
        subject: options.subjects.length,
      }) satisfies Record<AcademicSelectorField, number>,
    [catalogs.academicYears.length, options],
  );

  const fieldStates = useMemo(() => {
    return fields.map((field) =>
      resolveAcademicSelectorFieldState({
        field,
        enablePrograms,
        programsAvailable,
        selection,
        optionCount: optionCountByField[field],
        catalogLoading: loading,
        sectionsLoading,
        subjectsLoading,
        forceDisabled: disabled,
        hierarchyFailed,
        allowEmpty:
          field === "section"
            ? sectionOptional
            : field === "subject"
              ? subjectOptional
              : field === "group"
                ? groupOptional
                : field === "semester"
                  ? semesterOptional
                  : false,
      }),
    );
  }, [
    fields,
    enablePrograms,
    programsAvailable,
    selection,
    optionCountByField,
    loading,
    sectionsLoading,
    subjectsLoading,
    disabled,
    hierarchyFailed,
    sectionOptional,
    subjectOptional,
    groupOptional,
    semesterOptional,
  ]);

  const valueFor = (field: AcademicSelectorField): string | number => {
    switch (field) {
      case "academicYear":
        return selection.academicYearId ?? "";
      case "program":
        return selection.programId ?? "";
      case "course":
        return selection.courseId ?? "";
      case "group":
        return selection.groupId ?? "";
      case "semester":
        return selection.semesterId ?? "";
      case "section":
        return selection.sectionId ?? "";
      case "subject":
        return selection.subjectId ?? "";
    }
  };

  const onFieldChange = (field: AcademicSelectorField, raw: string) => {
    const id = raw === "" ? null : Number(raw);
    const value = id != null && Number.isFinite(id) ? id : null;
    switch (field) {
      case "academicYear":
        setSelection({ academicYearId: value });
        break;
      case "program":
        setSelection({ programId: value });
        break;
      case "course":
        setSelection({ courseId: value });
        break;
      case "group":
        setSelection({ groupId: value });
        break;
      case "semester":
        setSelection({ semesterId: value });
        break;
      case "section":
        setSelection({ sectionId: value });
        break;
      case "subject":
        setSelection({ subjectId: value });
        break;
    }
  };

  const menuItems = (field: AcademicSelectorField, allowEmpty: boolean) => {
    const emptyLabel =
      field === "section" ? "All sections (optional)" : field === "subject" ? "Select subject" : "All";
    const items: ReactNode[] = [];
    if (allowEmpty) {
      items.push(
        <MenuItem key="__empty" value="">
          <em>{emptyLabel}</em>
        </MenuItem>,
      );
    }

    switch (field) {
      case "academicYear":
        catalogs.academicYears.forEach((y) =>
          items.push(
            <MenuItem key={y.id} value={y.id}>
              {y.name}
              {y.isCurrent ? " (Current)" : ""}
            </MenuItem>,
          ),
        );
        break;
      case "program":
        options.programs.forEach((p) =>
          items.push(
            <MenuItem key={p.id} value={p.id}>
              {p.programName}
            </MenuItem>,
          ),
        );
        break;
      case "course":
        options.courses.forEach((c) =>
          items.push(
            <MenuItem key={c.id} value={c.id}>
              {c.code ? `${c.code} — ${c.name}` : c.name}
            </MenuItem>,
          ),
        );
        break;
      case "group":
        options.groups.forEach((g) =>
          items.push(
            <MenuItem key={g.id} value={g.id}>
              {g.name}
            </MenuItem>,
          ),
        );
        break;
      case "semester":
        options.semesters.forEach((s) =>
          items.push(
            <MenuItem key={s.id} value={s.id}>
              {s.name}
            </MenuItem>,
          ),
        );
        break;
      case "section":
        options.sections.forEach((s) =>
          items.push(
            <MenuItem key={s.id} value={s.id}>
              {s.sectionCode ? `${s.sectionCode} — ${s.sectionName}` : s.sectionName}
            </MenuItem>,
          ),
        );
        break;
      case "subject":
        options.subjects.forEach((s) =>
          items.push(
            <MenuItem key={s.id} value={s.id}>
              {s.code ? `${s.code} — ${s.name}` : s.name}
            </MenuItem>,
          ),
        );
        break;
    }
    return items;
  };

  const visibleStates = fieldStates.filter((s) => s.visible);
  const anyFieldLoading = visibleStates.some((s) => s.loading);

  return (
    <Box sx={sx}>
      {showError && error && (
        <Alert
          severity="error"
          sx={{ mb: 1.5 }}
          action={
            <Button color="inherit" size="small" disabled={loading} onClick={() => void refreshCatalogs()}>
              Retry
            </Button>
          }
        >
          {error}
        </Alert>
      )}
      {showError && !error && hierarchyFailed && enablePrograms && (
        <Alert
          severity="warning"
          sx={{ mb: 1.5 }}
          action={
            <Button color="inherit" size="small" disabled={loading} onClick={() => void refreshCatalogs()}>
              Retry
            </Button>
          }
        >
          Academic hierarchy could not be loaded. Course options remain hidden until refresh succeeds.
        </Alert>
      )}

      {loading && catalogs.academicYears.length === 0 ? (
        <Stack direction="row" spacing={1} sx={{ alignItems: "center", py: 1 }}>
          <CircularProgress size={18} />
          <Typography variant="body2" color="text.secondary">
            Loading academic catalogs…
          </Typography>
        </Stack>
      ) : (
        <Stack direction="row" spacing={1.5} useFlexGap sx={{ flexWrap: "wrap", alignItems: "flex-start" }}>
          {visibleStates.map((state) => {
            const allowEmpty =
              state.field === "section"
                ? sectionOptional
                : state.field === "subject"
                  ? subjectOptional
                  : state.field === "group"
                    ? groupOptional
                    : state.field === "semester"
                      ? semesterOptional
                      : false;

            return (
              <Box key={state.field} sx={{ minWidth: FIELD_MIN_WIDTH[state.field], maxWidth: 280 }}>
                <TextField
                  select
                  fullWidth
                  size={size}
                  id={`academic-scope-${state.field}`}
                  label={FIELD_LABEL[state.field]}
                  value={valueFor(state.field)}
                  disabled={state.disabled}
                  onChange={(e) => onFieldChange(state.field, String(e.target.value))}
                  slotProps={{
                    htmlInput: { "aria-label": FIELD_LABEL[state.field] },
                    input: {
                      endAdornment: state.loading ? (
                        <InputAdornment position="end" sx={{ mr: 2 }}>
                          <CircularProgress size={14} aria-label={`Loading ${FIELD_LABEL[state.field]}`} />
                        </InputAdornment>
                      ) : undefined,
                    },
                    select: {
                      displayEmpty: allowEmpty,
                    },
                  }}
                >
                  {menuItems(state.field, allowEmpty)}
                </TextField>
                {state.helperText && (
                  <FormHelperText error={state.empty && !state.loading}>{state.helperText}</FormHelperText>
                )}
              </Box>
            );
          })}
          {anyFieldLoading && !loading && (
            <Stack direction="row" spacing={0.75} sx={{ alignItems: "center", alignSelf: "center", minHeight: 40 }}>
              <CircularProgress size={14} />
              <Typography variant="caption" color="text.secondary">
                Updating options…
              </Typography>
            </Stack>
          )}
        </Stack>
      )}

      {showCascadeHint && (
        <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 1 }}>
          {cascadePath}
        </Typography>
      )}
    </Box>
  );
};

export default AcademicScopeSelector;
