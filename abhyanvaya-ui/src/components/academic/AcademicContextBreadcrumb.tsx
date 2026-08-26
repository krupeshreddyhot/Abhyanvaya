import { useEffect, useMemo, useState } from "react";
import { Alert, Breadcrumbs, Button, Skeleton, Typography } from "@mui/material";
import NavigateNextIcon from "@mui/icons-material/NavigateNext";
import { useAcademicUi } from "../../context/AcademicUiContext";
import {
  getAcademicContextBreadcrumb,
  hasAcademicContextSelection,
  type AcademicBreadcrumbItemDto,
  type AcademicOperationalContextQuery,
} from "../../services/academicBreadcrumbService";
import {
  academicContextQueryKey,
  toAcademicContextBreadcrumbQuery,
} from "../../utils/academicContextBreadcrumb";
import { getApiErrorMessage } from "../../utils/apiErrorMessage";

export type AcademicContextBreadcrumbProps = {
  /**
   * Optional override (e.g. Faculty current class). Merged over AcademicUi selection.
   * Display names still come from the Academic Breadcrumb API — never reconstructed here.
   */
  context?: AcademicOperationalContextQuery | null;
  /** When false, ignore AcademicUi and use only `context`. Default true. */
  useAcademicUiSelection?: boolean;
};

/**
 * AI29.1D Prompt 16 — shared academic context breadcrumb.
 * Consumes GET /api/v1/academic-structure/breadcrumb/context.
 */
export default function AcademicContextBreadcrumb({
  context = null,
  useAcademicUiSelection = true,
}: AcademicContextBreadcrumbProps) {
  const { selection } = useAcademicUi();

  const query = useMemo(
    () => toAcademicContextBreadcrumbQuery(useAcademicUiSelection ? selection : null, context),
    [
      useAcademicUiSelection,
      selection.programId,
      selection.courseId,
      selection.groupId,
      selection.semesterId,
      selection.sectionId,
      selection.sectionIds,
      selection.subjectId,
      context?.programId,
      context?.courseId,
      context?.groupId,
      context?.semesterId,
      context?.sectionId,
      context?.sectionIds,
      context?.subjectId,
    ],
  );

  const queryKey = academicContextQueryKey(query);
  const [items, setItems] = useState<AcademicBreadcrumbItemDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [retryTick, setRetryTick] = useState(0);

  useEffect(() => {
    if (!hasAcademicContextSelection(query)) {
      setItems((prev) => (prev.length === 0 ? prev : []));
      setLoading((prev) => (prev ? false : prev));
      setError((prev) => (prev == null ? prev : null));
      return;
    }

    let cancelled = false;
    setLoading(true);
    setError(null);
    void getAcademicContextBreadcrumb(query)
      .then((res) => {
        if (!cancelled) setItems(res.data?.items ?? []);
      })
      .catch((err) => {
        if (!cancelled) {
          setItems([]);
          setError(
            getApiErrorMessage(err, "Academic context could not be loaded.", {
              forbiddenFallback:
                "You are not authorized to load the academic context breadcrumb. Requires Attendance, Section, Timetable, Allocation, or Program.View access.",
            }),
          );
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [queryKey, retryTick]);

  if (!hasAcademicContextSelection(query)) return null;
  if (error) {
    return (
      <Alert
        severity="warning"
        variant="outlined"
        sx={{ py: 0.25 }}
        action={
          <Button color="inherit" size="small" onClick={() => setRetryTick((n) => n + 1)}>
            Retry
          </Button>
        }
      >
        {error}
      </Alert>
    );
  }
  if (loading && items.length === 0) {
    return <Skeleton variant="text" width="60%" height={28} aria-label="Loading academic context" />;
  }
  if (items.length === 0) return null;

  return (
    <Breadcrumbs
      aria-label="Academic context breadcrumb"
      separator={<NavigateNextIcon fontSize="small" />}
      sx={{
        "& .MuiBreadcrumbs-ol": { flexWrap: "wrap", rowGap: 0.5 },
      }}
    >
      {items.map((item, index) => {
        const last = index === items.length - 1;
        return (
          <Typography
            key={`${item.nodeId}-${index}`}
            variant="body2"
            color={last ? "text.primary" : "text.secondary"}
            sx={{ fontWeight: last ? 600 : 400 }}
          >
            {item.displayName}
          </Typography>
        );
      })}
    </Breadcrumbs>
  );
}
