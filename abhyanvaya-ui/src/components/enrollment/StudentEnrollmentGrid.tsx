import {
  Chip,
  Link,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import { useCallback, useEffect, useState } from "react";
import { enrollmentApiClient } from "../../api/enrollmentApiClient";
import type { EnrollmentFilters, StudentEnrollmentExplorerItem } from "../../types/enrollment";
import { getApiErrorMessage } from "../../utils/apiErrorMessage";

type Props = {
  batchId: string;
};

const StudentEnrollmentGrid = ({ batchId }: Props) => {
  const [rows, setRows] = useState<StudentEnrollmentExplorerItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(10);
  const [search, setSearch] = useState("");
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    const filters: EnrollmentFilters = { page: page + 1, pageSize, search: search || undefined };
    try {
      const res = await enrollmentApiClient.getBatchStudents(batchId, filters);
      setRows(res.data.items);
      setTotal(res.data.totalCount);
      setError(null);
    } catch (err) {
      setError(getApiErrorMessage(err));
    }
  }, [batchId, page, pageSize, search]);

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <Stack spacing={2}>
      <TextField
        size="small"
        label="Search students"
        value={search}
        onChange={(e) => {
          setPage(0);
          setSearch(e.target.value);
        }}
        slotProps={{ htmlInput: { "aria-label": "Search students in batch" } }}
      />
      {error ? (
        <Typography variant="body2" color="error">
          {error}
        </Typography>
      ) : null}
      <TableContainer component={Paper} variant="outlined">
        <Table size="small" aria-label="Student enrollment explorer">
          <TableHead>
            <TableRow>
              <TableCell>Student</TableCell>
              <TableCell>Photo</TableCell>
              <TableCell>Validation</TableCell>
              <TableCell>Embedding</TableCell>
              <TableCell>Upload</TableCell>
              <TableCell>Artifact</TableCell>
              <TableCell>Recognition</TableCell>
              <TableCell>Retries</TableCell>
              <TableCell>Failure</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((row) => (
              <TableRow key={row.itemId}>
                <TableCell>{row.studentNumber}</TableCell>
                <TableCell>{row.photoStatus}</TableCell>
                <TableCell>{row.validationStatus}</TableCell>
                <TableCell>{row.embeddingStatus}</TableCell>
                <TableCell>{row.uploadStatus}</TableCell>
                <TableCell>{row.artifactStatus}</TableCell>
                <TableCell>
                  <Chip
                    size="small"
                    label={row.recognitionReady ? "Ready" : "Pending"}
                    color={row.recognitionReady ? "success" : "default"}
                    variant="outlined"
                  />
                </TableCell>
                <TableCell>{row.retryCount}</TableCell>
                <TableCell>
                  {row.failureReason ? (
                    <Typography variant="caption" color="error">
                      {row.failureReason}
                    </Typography>
                  ) : row.downloadUrl ? (
                    <Link href={row.downloadUrl} target="_blank" rel="noopener noreferrer" variant="caption">
                      Photo URL
                    </Link>
                  ) : (
                    "—"
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
      <TablePagination
        component="div"
        count={total}
        page={page}
        onPageChange={(_, p) => setPage(p)}
        rowsPerPage={pageSize}
        onRowsPerPageChange={(e) => {
          setPageSize(Number.parseInt(e.target.value, 10));
          setPage(0);
        }}
        rowsPerPageOptions={[10, 25, 50]}
      />
    </Stack>
  );
};

export default StudentEnrollmentGrid;
