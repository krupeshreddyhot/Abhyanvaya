import { useEffect, useState } from "react";
import { Alert, Card, CardContent, Stack, Typography } from "@mui/material";
import { getContextDiagnostics } from "../../api/tenantContextApiClient";
import ContextAwareLayout from "../../layouts/ContextAwareLayout";
import { getApiErrorMessage } from "../../utils/apiErrorMessage";
import type { ContextDiagnosticsReport } from "../../types/tenantContext";

const ContextDiagnosticsPage = () => {
  const [report, setReport] = useState<ContextDiagnosticsReport | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void (async () => {
      try {
        const res = await getContextDiagnostics();
        setReport(res.data);
      } catch (err) {
        setError(getApiErrorMessage(err));
      }
    })();
  }, []);

  return (
    <ContextAwareLayout breadcrumbItems={[{ label: "Context Diagnostics" }]}>
      <Typography variant="h5">Context Diagnostics</Typography>
      <Typography variant="body2" color="text.secondary">
        Read-only operational context diagnostics for support engineers.
      </Typography>

      {error ? <Alert severity="error">{error}</Alert> : null}

      {report ? (
        <Card variant="outlined">
          <CardContent>
            <Stack spacing={1}>
              <Typography variant="body2">User ID: {report.userId}</Typography>
              <Typography variant="body2">Role: {report.role}</Typography>
              <Typography variant="body2">JWT Tenant ID: {report.jwtTenantId}</Typography>
              <Typography variant="body2">Persistence Provider: {report.persistenceProvider}</Typography>
              <Typography variant="body2">Context Exists: {String(report.contextExists)}</Typography>
              <Typography variant="body2">Expires UTC: {report.expiresUtc ?? "—"}</Typography>
              <Typography variant="body2">Remaining: {report.remainingTime ?? "—"}</Typography>
              <Typography variant="body2">Is Expired: {String(report.isExpired)}</Typography>
              <Typography variant="body2">Is Valid: {String(report.isValid)}</Typography>
              <Typography variant="body2">Context Source: {report.operationalContext?.contextSource ?? "—"}</Typography>
              <Typography variant="body2">Selected College: {report.operationalContext?.selectedCollegeName ?? "—"}</Typography>
              {report.validationErrors.length > 0 ? (
                <Typography variant="body2" color="error">
                  Validation: {report.validationErrors.join(", ")}
                </Typography>
              ) : null}
            </Stack>
          </CardContent>
        </Card>
      ) : null}
    </ContextAwareLayout>
  );
};

export default ContextDiagnosticsPage;
