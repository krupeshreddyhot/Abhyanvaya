import CameraswitchIcon from "@mui/icons-material/Cameraswitch";
import PhotoCameraIcon from "@mui/icons-material/PhotoCamera";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Typography,
} from "@mui/material";
import { useCallback, useEffect, useMemo, useState } from "react";
import { useBlurDetection } from "../../hooks/useBlurDetection";
import { useCameraDevices } from "../../hooks/useCameraDevices";
import { useCameraStream } from "../../hooks/useCameraStream";
import type { CapturedFrame, PhotoAcquisitionMethod } from "../../types/photoAcquisition";
import { captureFrameFromVideo } from "../../utils/classroomImageProcessing";
import { tryGetCaptureLocation } from "../../utils/geolocationCapture";

export type CameraCapturePanelProps = {
  mode: Extract<PhotoAcquisitionMethod, "capture" | "capture-multiple">;
  disabled?: boolean;
  busy?: boolean;
  onConfirmSingle: (frame: CapturedFrame, location: { latitude: number; longitude: number } | null) => void;
  onConfirmMultiSelection: (
    frame: CapturedFrame,
    location: { latitude: number; longitude: number } | null,
  ) => void;
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
}: CameraCapturePanelProps) => {
  const { devices, permission, error: deviceError, supported, requestPermission } = useCameraDevices();
  const [deviceId, setDeviceId] = useState<string>("");
  const [pendingFrame, setPendingFrame] = useState<CapturedFrame | null>(null);
  const [gallery, setGallery] = useState<CapturedFrame[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [capturing, setCapturing] = useState(false);
  const [captureError, setCaptureError] = useState<string | null>(null);
  const [location, setLocation] = useState<{ latitude: number; longitude: number } | null>(null);

  const streamEnabled = supported && permission === "granted" && !disabled && !busy;
  const { videoRef, mediaVideoRef, ready, error: streamError, stop } = useCameraStream({
    deviceId: deviceId || null,
    enabled: streamEnabled,
  });

  const selectedFrame = useMemo(
    () => gallery.find((frame) => frame.id === selectedId) ?? null,
    [gallery, selectedId],
  );

  const blurForPending = useBlurDetection(pendingFrame?.blurScore);
  const blurForSelected = useBlurDetection(selectedFrame?.blurScore);

  useEffect(() => {
    if (devices.length > 0 && !deviceId) {
      setDeviceId(devices[0].deviceId);
    }
  }, [devices, deviceId]);

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
      const geo = await tryGetCaptureLocation();
      setLocation(geo);
    }
  };

  const captureNow = useCallback(async () => {
    const video = mediaVideoRef.current;
    if (!video || !ready) {
      setCaptureError("Camera preview is not ready yet.");
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
    } catch (error) {
      setCaptureError(error instanceof Error ? error.message : "Capture failed.");
    } finally {
      setCapturing(false);
    }
  }, [location, mediaVideoRef, mode, ready]);

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

  const combinedError = captureError ?? streamError ?? deviceError;

  if (!supported || permission === "unsupported") {
    return (
      <Alert severity="warning">
        Camera capture is not supported in this browser. Use Upload Image, or try Chrome / Safari on a
        device with a camera.
      </Alert>
    );
  }

  if (permission !== "granted") {
    return (
      <Stack spacing={1.5}>
        <Alert severity="info">
          Allow camera access to capture a classroom photo. Works with desktop webcams, laptop cameras,
          Android Chrome, and iPhone Safari.
        </Alert>
        {combinedError && (
          <Alert severity="error" role="alert">
            {combinedError}
          </Alert>
        )}
        <Button
          variant="contained"
          startIcon={<PhotoCameraIcon />}
          onClick={() => void enableCamera()}
          disabled={disabled || busy}
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
          >
            {devices.map((device) => (
              <MenuItem key={device.deviceId} value={device.deviceId}>
                {device.label}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
        <Typography variant="caption" color="text.secondary">
          Live preview — hold steady and ensure faces are visible
        </Typography>
      </Stack>

      {!pendingFrame && (
        <Box
          sx={{
            position: "relative",
            borderRadius: 1,
            overflow: "hidden",
            border: 1,
            borderColor: "divider",
            bgcolor: "common.black",
            aspectRatio: "4 / 3",
            maxHeight: 420,
          }}
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
              <CircularProgress size={28} color="inherit" />
              <Typography variant="body2" sx={{ mt: 1 }}>
                Starting camera…
              </Typography>
            </Stack>
          )}
        </Box>
      )}

      {pendingFrame && mode === "capture" && (
        <Stack spacing={1.5}>
          <Box
            component="img"
            src={pendingFrame.previewUrl}
            alt="Captured classroom photo"
            sx={{
              width: "100%",
              maxHeight: 420,
              objectFit: "contain",
              borderRadius: 1,
              border: 1,
              borderColor: "divider",
              bgcolor: "background.default",
            }}
          />
          {blurForPending.warning && <Alert severity="warning">{blurForPending.warning}</Alert>}
          <Stack direction="row" spacing={1}>
            <Button variant="outlined" onClick={retakePending} disabled={disabled || busy}>
              Retake
            </Button>
            <Button
              variant="contained"
              onClick={() => onConfirmSingle(pendingFrame, location)}
              disabled={disabled || busy}
            >
              Use This Photo
            </Button>
          </Stack>
        </Stack>
      )}

      {mode === "capture-multiple" && (
        <Stack spacing={1.5}>
          <Stack direction="row" spacing={1}>
            <Button
              variant="contained"
              startIcon={<PhotoCameraIcon />}
              onClick={() => void captureNow()}
              disabled={disabled || busy || capturing || !ready}
            >
              {capturing ? "Capturing…" : "Capture Frame"}
            </Button>
            <Button
              variant="contained"
              color="secondary"
              disabled={disabled || busy || !selectedFrame}
              onClick={() => selectedFrame && onConfirmMultiSelection(selectedFrame, location)}
            >
              Use Selected Photo
            </Button>
          </Stack>

          {gallery.length > 0 && (
            <Stack spacing={1}>
              <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                Captured frames ({gallery.length}) — select one to upload
              </Typography>
              {blurForSelected.warning && <Alert severity="warning">{blurForSelected.warning}</Alert>}
              <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", gap: 1 }}>
                {gallery.map((frame, index) => (
                  <Box
                    key={frame.id}
                    onClick={() => setSelectedId(frame.id)}
                    sx={{
                      width: 96,
                      cursor: "pointer",
                      borderRadius: 1,
                      border: 2,
                      borderColor: selectedId === frame.id ? "primary.main" : "divider",
                      overflow: "hidden",
                    }}
                  >
                    <Box
                      component="img"
                      src={frame.previewUrl}
                      alt={`Capture ${index + 1}`}
                      sx={{ width: "100%", height: 72, objectFit: "cover", display: "block" }}
                    />
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
                ))}
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
        >
          {capturing ? "Capturing…" : "Capture Image"}
        </Button>
      )}

      {combinedError && (
        <Alert severity="error" role="alert">
          {combinedError}
        </Alert>
      )}
    </Stack>
  );
};

export default CameraCapturePanel;
