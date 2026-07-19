import {
  Autocomplete,
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  TextField,
  Typography,
} from "@mui/material";
import { useCallback, useEffect, useState } from "react";
import type { AvailableCollegeDto } from "../../api/tenantContextApiClient";
import { useTenantContext } from "../../context/TenantContextProvider";
import RecentCollegeList from "./RecentCollegeList";

type Props = {
  open: boolean;
  onSelected?: () => void;
};

const CollegeContextSelector = ({ open, onSelected }: Props) => {
  const { selectCollege, searchColleges, getRecentColleges, error } = useTenantContext();
  const [options, setOptions] = useState<AvailableCollegeDto[]>([]);
  const [recent, setRecent] = useState<Awaited<ReturnType<typeof getRecentColleges>>["recent"]>([]);
  const [popular, setPopular] = useState<Awaited<ReturnType<typeof getRecentColleges>>["popular"]>([]);
  const [inputValue, setInputValue] = useState("");
  const [selected, setSelected] = useState<AvailableCollegeDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const loadOptions = useCallback(
    async (search: string) => {
      setLoading(true);
      try {
        const items = await searchColleges(search);
        setOptions(items);
      } finally {
        setLoading(false);
      }
    },
    [searchColleges],
  );

  const loadRecent = useCallback(async () => {
    const data = await getRecentColleges();
    setRecent(data.recent);
    setPopular(data.popular);
  }, [getRecentColleges]);

  useEffect(() => {
    if (!open) return;
    void loadOptions("");
    void loadRecent();
  }, [open, loadOptions, loadRecent]);

  useEffect(() => {
    if (!open) return;
    const handle = window.setTimeout(() => {
      void loadOptions(inputValue);
    }, 300);
    return () => window.clearTimeout(handle);
  }, [inputValue, open, loadOptions]);

  const handleConfirm = async (collegeId?: number) => {
    const id = collegeId ?? selected?.id;
    if (!id) return;
    setSubmitting(true);
    const ok = await selectCollege(id);
    setSubmitting(false);
    if (ok) {
      onSelected?.();
    }
  };

  return (
    <Dialog open={open} fullWidth maxWidth="sm" aria-labelledby="college-context-selector-title">
      <DialogTitle id="college-context-selector-title">Select College Context</DialogTitle>
      <DialogContent>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          SuperAdmin operations require an operational college context. This is not stored in your JWT.
        </Typography>

        <RecentCollegeList
          recent={recent}
          popular={popular}
          onSelect={(collegeId) => void handleConfirm(collegeId)}
          selecting={submitting}
        />

        <Autocomplete
          options={options}
          value={selected}
          onChange={(_, value) => setSelected(value)}
          inputValue={inputValue}
          onInputChange={(_, value) => setInputValue(value)}
          getOptionLabel={(option) => `${option.name} (${option.code})`}
          isOptionEqualToValue={(a, b) => a.id === b.id}
          loading={loading}
          renderInput={(params) => (
            <TextField {...params} label="Search colleges" placeholder="Type college name or code" />
          )}
          renderOption={(props, option) => (
            <Box component="li" {...props} key={option.id}>
              <Box>
                <Typography variant="body2">{option.name}</Typography>
                <Typography variant="caption" color="text.secondary">
                  {option.code} · {option.status}
                  {option.universityName ? ` · ${option.universityName}` : ""}
                </Typography>
              </Box>
            </Box>
          )}
        />
        {error ? (
          <Typography variant="body2" color="error" sx={{ mt: 1 }}>
            {error}
          </Typography>
        ) : null}
      </DialogContent>
      <DialogActions>
        <Button onClick={() => void handleConfirm()} variant="contained" disabled={!selected || submitting}>
          Use This College
        </Button>
      </DialogActions>
    </Dialog>
  );
};

export default CollegeContextSelector;
