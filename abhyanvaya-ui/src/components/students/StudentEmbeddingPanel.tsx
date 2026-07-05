import AutoFixHighIcon from "@mui/icons-material/AutoFixHigh";
import RefreshIcon from "@mui/icons-material/Refresh";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Stack,
  Typography,
} from "@mui/material";
import { useCallback, useEffect, useState } from "react";
import {
  embeddingQualityLabel,
  embeddingStatusLabel,
  generateStudentEmbedding,
  getStudentEmbeddingStatus,
  regenerateStudentEmbedding,
  type StudentFaceEmbeddingStatusDto,
} from "../../services/studentEmbeddingService";

export type StudentEmbeddingPanelProps = {
  studentId: number;
  photoUrl?: string | null;
  photoAlt?: string;
  disabled?: boolean;
};

const embeddingStatusChipColor = (
  status: StudentFaceEmbeddingStatusDto
): "default" | "warning" | "success" | "error" | "info" => {
  if (status.generationPending) return "warning";
  if (!status.hasActiveEmbedding) {
    const lifecycle = status.activeStatus;
    if (lifecycle === "Failed" || lifecycle === 3) return "error";
    return "default";
  }

  const lifecycle = status.activeStatus;
  if (lifecycle === "Failed" || lifecycle === 3) return "error";
  if (lifecycle === "Pending" || lifecycle === 0) return "warning";
  if (lifecycle === "Processing" || lifecycle === 1) return "info";
  if (lifecycle === "Completed" || lifecycle === 2) return "success";
  return "default";
};

const lifecycleLabel = (status: StudentFaceEmbeddingStatusDto): string => {
  if (status.generationPending) return "Generation Queued";
  if (!status.hasPhoto) return "Photo Required";
  if (!status.hasActiveEmbedding) {
    return embeddingStatusLabel(status.activeStatus) || "No Active Embedding";
  }
  return embeddingStatusLabel(status.activeStatus);
};

const formatGeneratedUtc = (value?: string | null): string | null => {
  if (!value) return null;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return null;
  return date.toLocaleString();
};

export const StudentEmbeddingPanel = ({
  studentId,
  photoUrl,
  photoAlt = "Student photo",
  disabled = false,
}: StudentEmbeddingPanelProps) => {
  const [status, setStatus] = useState<StudentFaceEmbeddingStatusDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [generating, setGenerating] = useState(false);
  const [regenerating, setRegenerating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadStatus = useCallback(async () => {
    if (studentId <= 0) return;
    setLoading(true);
    setError(null);
    try {
      const res = await getStudentEmbeddingStatus(studentId);
      setStatus(res.data);
    } catch {
      setStatus(null);
      setError("Could not load embedding status.");
    } finally {
      setLoading(false);
    }
  }, [studentId]);

  useEffect(() => {
    void loadStatus();
  }, [loadStatus]);

  const handleGenerate = async () => {
    setGenerating(true);
    setError(null);
    try {
      const res = await generateStudentEmbedding(studentId);
      setStatus(res.data);
    } catch {
      setError("Failed to queue embedding generation.");
    } finally {
      setGenerating(false);
    }
  };

  const handleRegenerate = async () => {
    setRegenerating(true);
    setError(null);
    try {
      const res = await regenerateStudentEmbedding(studentId);
      setStatus(res.data);
    } catch {
      setError("Failed to queue embedding regeneration.");
    } finally {
      setRegenerating(false);
    }
  };

  const busy = generating || regenerating || loading;
  const generatedLabel = formatGeneratedUtc(status?.generatedUtc);
  const canGenerate = status?.hasPhoto && !busy && !disabled;

  return (
    <Card variant="outlined" sx={{ minWidth: 0 }}>
      <CardContent sx={{ p: { xs: 1.5, sm: 2 } }}>
        <Stack spacing={1.5}>
          <Typography variant="h6" component="h3">
            Face Embedding
          </Typography>

          {photoUrl && (
            <Box
              component="img"
              src={photoUrl}
              alt={photoAlt}
              sx={{
                maxHeight: 160,
                maxWidth: "100%",
                objectFit: "contain",
                borderRadius: 1,
                alignSelf: "center",
              }}
            />
          )}

          {loading && !status ? (
            <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
              <CircularProgress size={20} />
              <Typography variant="body2" color="text.secondary">
                Loading embedding status…
              </Typography>
            </Stack>
          ) : (
            <>
              <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", gap: 1 }}>
                <Chip
                  label={status ? lifecycleLabel(status) : "Unknown"}
                  size="small"
                  color={status ? embeddingStatusChipColor(status) : "default"}
                  variant={status?.hasActiveEmbedding ? "filled" : "outlined"}
                />
                {status?.activeQuality != null && (
                  <Chip
                    label={`Quality: ${embeddingQualityLabel(status.activeQuality)}`}
                    size="small"
                    variant="outlined"
                  />
                )}
                {status?.activeModel && (
                  <Chip label={status.activeModel} size="small" variant="outlined" />
                )}
              </Stack>

              {generatedLabel && (
                <Typography variant="caption" color="text.secondary">
                  Generated: {generatedLabel}
                </Typography>
              )}

              {status?.activeDimension != null && status.activeDimension > 0 && (
                <Typography variant="caption" color="text.secondary">
                  Dimensions: {status.activeDimension}
                </Typography>
              )}

              {status?.isPhotoVersionStale && (
                <Alert severity="warning" sx={{ py: 0 }}>
                  Photo was updated after the active embedding was generated. Regenerate recommended.
                </Alert>
              )}

              {status && status.retryCount > 0 && (
                <Typography variant="caption" color="text.secondary">
                  Retry attempts: {status.retryCount}
                </Typography>
              )}

              {status && (
                <Typography variant="caption" color="text.secondary">
                  Total embeddings: {status.totalEmbeddings}
                </Typography>
              )}
            </>
          )}

          <Stack direction={{ xs: "column", sm: "row" }} spacing={1}>
            <Button
              variant="contained"
              startIcon={
                generating ? (
                  <CircularProgress size={18} color="inherit" />
                ) : (
                  <AutoFixHighIcon />
                )
              }
              onClick={() => void handleGenerate()}
              disabled={!canGenerate}
              fullWidth
            >
              Generate Embedding
            </Button>
            <Button
              variant="outlined"
              startIcon={
                regenerating ? (
                  <CircularProgress size={18} color="inherit" />
                ) : (
                  <RefreshIcon />
                )
              }
              onClick={() => void handleRegenerate()}
              disabled={!canGenerate || !status?.hasActiveEmbedding}
              fullWidth
            >
              Regenerate
            </Button>
          </Stack>

          {!status?.hasPhoto && (
            <Typography variant="body2" color="text.secondary">
              Upload a student photo before generating an embedding.
            </Typography>
          )}

          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </CardContent>
    </Card>
  );
};

export default StudentEmbeddingPanel;
