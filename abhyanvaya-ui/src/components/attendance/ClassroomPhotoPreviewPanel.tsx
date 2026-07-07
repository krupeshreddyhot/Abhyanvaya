import DeleteIcon from "@mui/icons-material/Delete";
import CloudUploadIcon from "@mui/icons-material/CloudUpload";
import {
  Box,
  Button,
  Card,
  CardContent,
  Grid,
  Stack,
  Typography,
} from "@mui/material";
import {
  formatFileSizeLabel,
  formatResolution,
  formatUploadedTimestamp,
} from "../../utils/fileDisplay";
import type { UploadState } from "../../types/uploadState";

export type ClassroomPhotoPreviewPanelProps = {
  uploadState: UploadState;
  disabled?: boolean;
  busy?: boolean;
  onReplace: () => void;
  onDelete: () => void;
};

type MetadataRowProps = {
  label: string;
  value: string;
};

const MetadataRow = ({ label, value }: MetadataRowProps) => (
  <Stack spacing={0.25} sx={{ py: 0.65 }}>
    <Typography variant="caption" color="text.secondary" sx={{ lineHeight: 1.2 }}>
      {label}
    </Typography>
    <Typography variant="body2" sx={{ fontWeight: 600, lineHeight: 1.4, wordBreak: "break-word" }}>
      {value}
    </Typography>
  </Stack>
);

export const ClassroomPhotoPreviewPanel = ({
  uploadState,
  disabled = false,
  busy = false,
  onReplace,
  onDelete,
}: ClassroomPhotoPreviewPanelProps) => {
  const isDisabled = disabled || busy;

  return (
    <Card variant="outlined" aria-label="Classroom photo preview">
      <CardContent sx={{ px: { xs: 2, sm: 2.5 }, py: 2 }}>
        <Grid container spacing={2.5} sx={{ alignItems: "stretch" }}>
          <Grid size={{ xs: 12, sm: 5, md: 5 }}>
            <Stack spacing={1.5} sx={{ height: "100%" }}>
              <Typography variant="subtitle2" component="h3" sx={{ fontWeight: 700 }}>
                Image Preview
              </Typography>
              {uploadState.previewUrl && (
                <Box
                  sx={{
                    flex: 1,
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    bgcolor: "background.default",
                    borderRadius: 1,
                    border: 1,
                    borderColor: "divider",
                    p: 1.5,
                    minHeight: { xs: 200, sm: 280 },
                  }}
                >
                  <Box
                    component="img"
                    src={uploadState.previewUrl}
                    alt="Classroom photo preview"
                    sx={{
                      maxWidth: "100%",
                      maxHeight: { xs: 220, sm: 320 },
                      objectFit: "contain",
                      borderRadius: 1,
                    }}
                  />
                </Box>
              )}
            </Stack>
          </Grid>

          <Grid size={{ xs: 12, sm: 7, md: 7 }}>
            <Stack spacing={1.5} sx={{ height: "100%" }}>
              <Typography variant="subtitle2" component="h3" sx={{ fontWeight: 700 }}>
                File Information
              </Typography>

              <Stack spacing={0}>
                <MetadataRow label="Filename" value={uploadState.fileName ?? "—"} />
                <MetadataRow
                  label="Resolution"
                  value={formatResolution(uploadState.imageWidth, uploadState.imageHeight)}
                />
                <MetadataRow label="Size" value={formatFileSizeLabel(uploadState.fileSize)} />
                <MetadataRow
                  label="Uploaded Time"
                  value={formatUploadedTimestamp(uploadState.uploadedAt)}
                />
                <MetadataRow label="Estimated Faces" value="Waiting for AI detection" />
              </Stack>

              <Stack direction={{ xs: "column", sm: "row" }} spacing={1} sx={{ pt: 0.5, mt: "auto" }}>
                <Button
                  variant="contained"
                  startIcon={<CloudUploadIcon />}
                  onClick={onReplace}
                  disabled={isDisabled}
                  fullWidth
                  aria-label="Replace classroom photo"
                >
                  Replace
                </Button>
                <Button
                  variant="outlined"
                  color="error"
                  startIcon={<DeleteIcon />}
                  onClick={onDelete}
                  disabled={isDisabled}
                  fullWidth
                  aria-label="Delete classroom photo"
                >
                  Delete
                </Button>
              </Stack>
            </Stack>
          </Grid>
        </Grid>
      </CardContent>
    </Card>
  );
};

export default ClassroomPhotoPreviewPanel;
