import { Alert, List, ListItem, ListItemText, Typography } from "@mui/material";
import { useCallback, useEffect, useState } from "react";
import type { AxiosResponse } from "axios";
import type { EnrollmentFilters, PagedResult, StudentEnrollmentExplorerItem } from "../../types/enrollment";
import { getApiErrorMessage } from "../../utils/apiErrorMessage";

type Props = {
  batchId: string;
  fetchStudents: (filters: EnrollmentFilters) => Promise<AxiosResponse<PagedResult<StudentEnrollmentExplorerItem>>>;
};

const FailurePanel = ({ batchId, fetchStudents }: Props) => {
  const [failures, setFailures] = useState<StudentEnrollmentExplorerItem[]>([]);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const res = await fetchStudents({ page: 1, pageSize: 200, search: undefined });
      setFailures(res.data.items.filter((x) => x.failureReason));
      setError(null);
    } catch (err) {
      setError(getApiErrorMessage(err));
    }
  }, [fetchStudents]);

  useEffect(() => {
    void load();
  }, [load, batchId]);

  if (error) {
    return <Alert severity="error">{error}</Alert>;
  }

  if (failures.length === 0) {
    return (
      <Typography variant="body2" color="text.secondary">
        No failed students in this batch.
      </Typography>
    );
  }

  return (
    <List dense aria-label="Batch failures">
      {failures.map((item) => (
        <ListItem key={item.itemId} divider>
          <ListItemText
            primary={item.studentNumber}
            secondary={`${item.failureReason ?? "Unknown error"} · Retries: ${item.retryCount}`}
          />
        </ListItem>
      ))}
    </List>
  );
};

export default FailurePanel;
