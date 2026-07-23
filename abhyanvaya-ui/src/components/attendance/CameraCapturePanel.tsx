import CameraswitchIcon from "@mui/icons-material/Cameraswitch";
import PhotoCameraIcon from "@mui/icons-material/PhotoCamera";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Typography,
} from "@mui/material";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useBlurDetection } from "../../hooks/useBlurDetection";
import { useCameraDevices } from "../../hooks/useCameraDevices";
import { useCameraStream } from "../../hooks/useCameraStream";
import type { CapturedFrame, PhotoAcquisitionMethod } from "../../types/photoAcquisition";
import { getCameraStatusView } from "../../utils/cameraStatusMessages";
import { captureFrameFromVideo } from "../../utils/classroomImageProcessing";
import { getImageQualityIndicator } from "../../utils/imageQuality";
import { tryGetCaptureLocation } from "../../utils/geolocationCapture";
import { CaptureSuccessCard } from "./CaptureSuccessCard";

export type CameraCapturePanelProps = {
  mode: Extract<PhotoAcquisitionMethod, "capture" | "capture-multiple">;
  disabled?: boolean;
  busy?: boolean;
  onConfirmSingle: (frame: CapturedFrame, location: { latitude: number; longitude: number } | null) => void;
  onConfirmMultiSelection: (
    frame: CapturedFrame,
    location: { latitude: number; longitude: number } | null,
  ) => void;
  onConfirmMultiAll?: (
    frames: CapturedFrame[],
    location: { latitude: number; longitude: number } | null,
  ) => void;
  onNotify?: (message: string, severity?: "success" | "info" | "warning" | "error") => void;
};

const createFrameId = (): string =>
  typeof crypto !== "undefined" && "randomUUID" in crypto
    ? crypto.randomUUID()
    : `frame-${Date.now()}-${Math.random().toString(16).slice(2)}`;

export const CameraCapturePanel = ({
  mode,
  disabled = false,
  busy = false,
  onConfirmSingle,
  onConfirmMultiSelection,
  onConfirmMultiAll,
  onNotify,
}: CameraCapturePanelProps) => {
  const { devices, permission, error: deviceError, supported, requestPermission } = useCameraDevices();
  const [deviceId, setDeviceId] = useState<string>("");
  const [pendingFrame, setPendingFrame] = useState<CapturedFrame | null>(null);
  const [gallery, setGallery] = useState<CapturedFrame[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [capturing, setCapturing] = useState(false);
  const [captureError, setCaptureError] = useState<string | null>(null);
  const [location, setLocation] = useState<{ latitude: number; longitude: number } | null>(null);
  const notifiedReadyRef = useRef(false);

  const streamEnabled = supported && permission === "granted" && !disabled && !busy;
  const { videoRef, mediaVideoRef, ready, error: streamError, stop } = useCameraStream({
    deviceId: deviceId || null,
    enabled: streamEnabled,
  });

  const selectedDeviceLabel = devices.find((device) => device.deviceId === deviceId)?.label;
  const cameraStatus = getCameraStatusView({
    supported,
    permission,
    ready,
    streamError,
    deviceError,
    deviceLabel: selectedDeviceLabel,
  });

  const selectedFrame = useMemo(
    () => gallery.find((frame) => frame.id === selectedId) ?? null,
    [gallery, selectedId],
  );

  const blurForSelected = useBlurDetection(selectedFrame?.blurScore);

  useEffect(() => {
    if (devices.length > 0 && !deviceId) {
      setDeviceId(devices[0].deviceId);
    }
  }, [devices, deviceId]);

  useEffect(() => {
    if (ready && permission === "granted" && !notifiedReadyRef.current) {
      notifiedReadyRef.current = true;
      onNotify?.("Camera Ready", "success");
    }
    if (!ready) {
      notifiedReadyRef.current = false;
    }
  }, [onNotify, permission, ready]);

  useEffect(() => {
    return () => {
      stop();
      if (pendingFrame?.previewUrl.startsWith("blob:")) {
        URL.revokeObjectURL(pendingFrame.previewUrl);
      }
      gallery.forEach((frame) => {
        if (frame.previewUrl.startsWith("blob:")) {
          URL.revokeObjectURL(frame.previewUrl);
        }
      });
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- cleanup on unmount only
  }, []);

  const enableCamera = async () => {
    setCaptureError(null);
    const ok = await requestPermission();
    if (ok) {
      onNotify?.("Camera Ready", "success");
      const geo = await tryGetCaptureLocation();
      setLocation(geo);
    }
  };

  const captureNow = useCallback(async () => {
    const video = mediaVideoRef.current;
    if (!video || !ready) {
      setCaptureError("Camera preview is not ready yet. Wait for Camera Ready, then try again.");
      return;
    }

    setCapturing(true);
    setCaptureError(null);

    try {
      if (!location) {
        const geo = await tryGetCaptureLocation();
        setLocation(geo);
      }

      const processed = await captureFrameFromVideo(video);
      const frame: CapturedFrame = {
        id: createFrameId(),
        file: processed.file,
        previewUrl: URL.createObjectURL(processed.file),
        width: processed.width,
        height: processed.height,
        blurScore: processed.blurScore,
        capturedAt: new Date(),
      };

      if (mode === "capture") {
        setPendingFrame((current) => {
          if (current?.previewUrl.startsWith("blob:")) {
            URL.revokeObjectURL(current.previewUrl);
          }
          return frame;
        });
      } else {
        setGallery((current) => [...current, frame]);
        setSelectedId(frame.id);
      }
      onNotify?.("Photo Captured", "success");
    } catch {
      setCaptureError("Unable to capture photo. Hold steady and try again.");
      onNotify?.("Capture failed", "error");
    } finally {
      setCapturing(false);
    }
  }, [location, mediaVideoRef, mode, onNotify, ready]);

  const retakePending = () => {
    setPendingFrame((current) => {
      if (current?.previewUrl.startsWith("blob:")) {
        URL.revokeObjectURL(current.previewUrl);
      }
      return null;
    });
  };

  const removeGalleryFrame = (id: string) => {
    setGallery((current) => {
      const target = current.find((frame) => frame.id === id);
      if (target?.previewUrl.startsWith("blob:")) {
        URL.revokeObjectURL(target.previewUrl);
      }
      const next = current.filter((frame) => frame.id !== id);
      if (selectedId === id) {
        setSelectedId(next[next.length - 1]?.id ?? null);
      }
      return next;
    });
  };

  if (!supported || permission === "unsupported") {
    return (
      <Alert severity="warning" role="status">
        {cameraStatus.message}
      </Alert>
    );
  }

  if (permission !== "granted") {
    return (
      <Stack spacing={1.5}>
        <Chip
          label={cameraStatus.title}
          color="warning"
          size="small"
          sx={{ alignSelf: "flex-start" }}
        />
        <Alert severity="info" role="status">
          {cameraStatus.message}
        </Alert>
        {captureError && (
          <Alert severity="error" role="alert">
            {captureError}
          </Alert>
        )}
        <Button
          variant="contained"
          startIcon={<PhotoCameraIcon />}
          onClick={() => void enableCamera()}
          disabled={disabled || busy}
          aria-label="Enable camera permission"
        >
          Enable Camera
        </Button>
      </Stack>
    );
  }

  return (
    <Stack spacing={2}>
      <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} sx={{ alignItems: { sm: "center" } }}>
        <FormControl size="small" sx={{ minWidth: 220 }} disabled={disabled || busy || capturing}>
          <InputLabel id="camera-device-label">Camera</InputLabel>
          <Select
            labelId="camera-device-label"
            label="Camera"
            value={deviceId}
            onChange={(event) => setDeviceId(event.target.value)}
            startAdornment={<CameraswitchIcon fontSize="small" sx={{ mr: 1, color: "text.secondary" }} />}
            inputProps={{ "aria-label": "Select camera device" }}
          >
            {devices.map((device) => (
              <MenuItem key={device.deviceId} value={device.deviceId}>
                {device.label}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
        <Chip
          size="small"
          label={cameraStatus.title}
          color={
            cameraStatus.tone === "success"
              ? "success"
              : cameraStatus.tone === "error"
                ? "error"
                : cameraStatus.tone === "warning"
                  ? "warning"
                  : "info"
          }
          aria-live="polite"
        />
        <Typography variant="caption" color="text.secondary">
          {cameraStatus.message}
        </Typography>
      </Stack>

      {!pendingFrame && (
        <Box
          sx={{
            position: "relative",
            borderRadius: 1,
            overflow: "hidden",
            border: 1,
            borderColor: ready ? "success.main" : "divider",
            bgcolor: "common.black",
            aspectRatio: "4 / 3",
            maxHeight: 420,
          }}
          aria-label={ready ? "Live camera preview" : "Camera loading"}
        >
          <Box
            component="video"
            ref={videoRef}
            autoPlay
            playsInline
            muted
            sx={{ width: "100%", height: "100%", objectFit: "cover" }}
          />
          {!ready && (
            <Stack
              sx={{
                position: "absolute",
                inset: 0,
                alignItems: "center",
                justifyContent: "center",
                color: "common.white",
              }}
            >
              <CircularProgress size={28} color="inherit" aria-label="Camera loading" />
              <Typography variant="body2" sx={{ mt: 1 }}>
                Camera Loading…
              </Typography>
            </Stack>
          )}
        </Box>
      )}

      {pendingFrame && mode === "capture" && (
        <CaptureSuccessCard
          frame={pendingFrame}
          disabled={disabled}
          busy={busy}
          onRetake={retakePending}
          onConfirm={() => onConfirmSingle(pendingFrame, location)}
          confirmLabel="Use This Photo"
        />
      )}

      {mode === "capture-multiple" && (
        <Stack spacing={1.5}>
          <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap" }}>
            <Button
              variant="contained"
              startIcon={<PhotoCameraIcon />}
              onClick={() => void captureNow()}
              disabled={disabled || busy || capturing || !ready}
              aria-label="Capture frame"
            >
              {capturing ? "Capturing…" : "Capture Frame"}
            </Button>
            <Button
              variant="contained"
              color="secondary"
              disabled={disabled || busy || gallery.length === 0}
              aria-label={
                onConfirmMultiAll
                  ? `Add ${gallery.length} photos to session`
                  : "Use selected photo"
              }
              onClick={() => {
                if (onConfirmMultiAll) {
                  onConfirmMultiAll(gallery, location);
                  return;
                }
                if (selectedFrame) {
                  onConfirmMultiSelection(selectedFrame, location);
                }
              }}
            >
              {onConfirmMultiAll
                ? `Add ${gallery.length || ""} Photo${gallery.length === 1 ? "" : "s"}`
                : "Use Selected Photo"}
            </Button>
          </Stack>

          {gallery.length > 0 && (
            <Stack spacing={1}>
              <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                Captured frames ({gallery.length}) — confirm to upload
              </Typography>
              {blurForSelected.warning && (
                <Alert severity="warning">{getImageQualityIndicator(selectedFrame?.blurScore).label}: consider retaking.</Alert>
              )}
              <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", gap: 1 }}>
                {gallery.map((frame, index) => {
                  const quality = getImageQualityIndicator(frame.blurScore);
                  return (
                    <Box
                      key={frame.id}
                      onClick={() => setSelectedId(frame.id)}
                      onKeyDown={(event) => {
                        if (event.key === "Enter" || event.key === " ") {
                          event.preventDefault();
                          setSelectedId(frame.id);
                        }
                      }}
                      role="button"
                      tabIndex={0}
                      aria-pressed={selectedId === frame.id}
                      aria-label={`Captured frame ${index + 1}, quality ${quality.label}`}
                      sx={{
                        width: 112,
                        cursor: "pointer",
                        borderRadius: 1,
                        border: 2,
                        borderColor: selectedId === frame.id ? "primary.main" : "divider",
                        overflow: "hidden",
                        outlineOffset: 2,
                        "&:focus-visible": {
                          outline: (theme) => `2px solid ${theme.palette.primary.main}`,
                        },
                      }}
                    >
                      <Box
                        component="img"
                        src={frame.previewUrl}
                        alt={`Capture ${index + 1}`}
                        sx={{ width: "100%", height: 72, objectFit: "cover", display: "block" }}
                      />
                      <Typography variant="caption" sx={{ display: "block", px: 0.5, py: 0.25 }}>
                        {quality.stars}
                      </Typography>
                      <Button
                        size="small"
                        fullWidth
                        onClick={(event) => {
                          event.stopPropagation();
                          removeGalleryFrame(frame.id);
                        }}
                      >
                        Remove
                      </Button>
                    </Box>
                  );
                })}
              </Stack>
            </Stack>
          )}
        </Stack>
      )}

      {mode === "capture" && !pendingFrame && (
        <Button
          variant="contained"
          startIcon={<PhotoCameraIcon />}
          onClick={() => void captureNow()}
          disabled={disabled || busy || capturing || !ready}
          aria-label="Capture image"
        >
          {capturing ? "Capturing…" : "Capture Image"}
        </Button>
      )}

      {(captureError || streamError) && (
        <Alert severity="error" role="alert">
          {captureError ??
            (streamError
              ? "Unable to start the camera preview. Check permissions or try another camera."
              : null)}
        </Alert>
      )}
    </Stack>
  );
};

export default CameraCapturePanel;
