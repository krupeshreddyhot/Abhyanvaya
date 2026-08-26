import { useCallback, useEffect, useRef, useState } from "react";
import { Autocomplete, Box, Button, CircularProgress, Stack, TextField, Typography } from "@mui/material";
import { listStaff, type StaffListItem } from "../../services/setupService";
import { getApiErrorMessage } from "../../utils/apiErrorMessage";
import { isAbortError, replaceAbortController } from "../../utils/academicRequest";
import {
  facultyIdForAssign,
  formatFacultyOptionLabel,
  formatFacultySelectionSummary,
} from "../../utils/facultyStaffSelector";

type Props = {
  value: StaffListItem | null;
  onChange: (faculty: StaffListItem | null) => void;
  disabled?: boolean;
  /** Page size for search results — avoids loading the full staff population. */
  pageSize?: number;
};

const errMsg = (e: unknown): string =>
  getApiErrorMessage(e, "Could not load faculty.", {
    forbiddenFallback: "You are not authorized to list faculty for allocation.",
  });

/**
 * Enterprise Faculty selector — Search / Select over existing GET /api/staff.
 * Submits Staff Id via parent; does not invent a Faculty entity.
 */
export function FacultyStaffSelector({ value, onChange, disabled, pageSize = 25 }: Props) {
  const [open, setOpen] = useState(false);
  const [inputValue, setInputValue] = useState("");
  const [options, setOptions] = useState<StaffListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [total, setTotal] = useState(0);
  const abortRef = useRef<AbortController | null>(null);

  const load = useCallback(
    async (search: string) => {
      const controller = replaceAbortController(abortRef.current);
      abortRef.current = controller;
      setLoading(true);
      setError(null);
      try {
        const res = await listStaff(
          {
            search: search.trim() || undefined,
            page: 1,
            pageSize,
          },
          { signal: controller.signal },
        );
        if (controller.signal.aborted) return;
        setOptions(res.data?.items ?? []);
        setTotal(res.data?.total ?? 0);
      } catch (e) {
        if (isAbortError(e)) return;
        setOptions([]);
        setTotal(0);
        setError(errMsg(e));
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    },
    [pageSize],
  );

  useEffect(() => {
    if (!open) return;
    const handle = window.setTimeout(() => {
      void load(inputValue);
    }, 300);
    return () => {
      window.clearTimeout(handle);
      abortRef.current?.abort();
    };
  }, [open, inputValue, load]);

  const summary = value ? formatFacultySelectionSummary(value) : null;
  const selectedId = facultyIdForAssign(value);

  return (
    <Stack spacing={0.75} sx={{ minWidth: { xs: "100%", sm: 280 } }}>
      <Autocomplete
        size="small"
        open={open}
        onOpen={() => setOpen(true)}
        onClose={() => setOpen(false)}
        options={options}
        value={value}
        onChange={(_, next) => onChange(next)}
        inputValue={inputValue}
        onInputChange={(_, next, reason) => {
          if (reason === "input" || reason === "clear") setInputValue(next);
        }}
        filterOptions={(x) => x}
        getOptionLabel={(o) => formatFacultyOptionLabel(o)}
        isOptionEqualToValue={(a, b) => a.id === b.id}
        loading={loading}
        disabled={disabled}
        noOptionsText={loading ? "Loading…" : error ? "Faculty unavailable" : "No faculty found"}
        renderOption={(props, option) => {
          const { key, ...rest } = props as typeof props & { key?: string };
          const s = formatFacultySelectionSummary(option);
          return (
            <li key={key ?? option.id} {...rest}>
              <Box sx={{ py: 0.25 }}>
                <Typography variant="body2">{s.name}</Typography>
                <Typography variant="caption" color="text.secondary">
                  Staff ID {s.staffId}
                </Typography>
              </Box>
            </li>
          );
        }}
        renderInput={(params) => (
          <TextField
            {...params}
            label="Faculty"
            placeholder="Search / Select Faculty"
            error={Boolean(error)}
            slotProps={{
              ...params.slotProps,
              input: {
                ...params.slotProps.input,
                endAdornment: (
                  <>
                    {loading ? <CircularProgress color="inherit" size={16} /> : null}
                    {params.slotProps.input.endAdornment}
                  </>
                ),
              },
            }}
          />
        )}
      />

      {summary && selectedId != null ? (
        <Typography variant="caption" color="text.secondary" component="div">
          <strong>{summary.name}</strong>
          <br />
          Staff ID {summary.staffId}
        </Typography>
      ) : null}

      {error ? (
        <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap", alignItems: "center" }}>
          <Typography variant="caption" color="error">
            {error}
          </Typography>
          <Button size="small" onClick={() => void load(inputValue)} disabled={loading || disabled}>
            Retry
          </Button>
        </Stack>
      ) : null}

      {!error && open && !loading && options.length === 0 ? (
        <Typography variant="caption" color="text.secondary">
          No faculty match this search.
        </Typography>
      ) : null}

      {!error && total > pageSize ? (
        <Typography variant="caption" color="text.secondary">
          Showing {options.length} of {total}. Refine search to narrow results.
        </Typography>
      ) : null}
    </Stack>
  );
}
