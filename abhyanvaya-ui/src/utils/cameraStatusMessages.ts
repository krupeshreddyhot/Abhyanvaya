import type { CameraPermissionState } from "../hooks/useCameraDevices";

export type CameraUxStatus = "unsupported" | "permission" | "loading" | "ready" | "connected" | "error";

export type CameraStatusView = {
  status: CameraUxStatus;
  title: string;
  message: string;
  tone: "info" | "success" | "warning" | "error" | "default";
};

export const getCameraStatusView = (input: {
  supported: boolean;
  permission: CameraPermissionState;
  ready: boolean;
  streamError: string | null;
  deviceError: string | null;
  deviceLabel?: string | null;
}): CameraStatusView => {
  if (!input.supported || input.permission === "unsupported") {
    return {
      status: "unsupported",
      title: "Camera Unavailable",
      message: "This browser does not support camera capture. Use Upload Image instead.",
      tone: "warning",
    };
  }

  if (input.permission === "denied") {
    return {
      status: "permission",
      title: "Camera Permission Needed",
      message:
        "Allow camera access in your browser settings, then return here and try again. You can also use Upload Image.",
      tone: "warning",
    };
  }

  if (input.streamError || input.deviceError) {
    return {
      status: "error",
      title: "Camera Error",
      message:
        input.streamError ||
        input.deviceError ||
        "Something went wrong with the camera. Try another device or use Upload Image.",
      tone: "error",
    };
  }

  if (input.permission === "prompt" || input.permission === "unknown") {
    return {
      status: "permission",
      title: "Enable Camera",
      message: "Grant camera permission to capture classroom photos for attendance.",
      tone: "info",
    };
  }

  if (!input.ready) {
    return {
      status: "loading",
      title: "Camera Loading",
      message: "Starting camera preview… Hold steady once the live view appears.",
      tone: "info",
    };
  }

  if (input.deviceLabel) {
    return {
      status: "connected",
      title: "Camera Connected",
      message: `Live preview ready · ${input.deviceLabel}`,
      tone: "success",
    };
  }

  return {
    status: "ready",
    title: "Camera Ready",
    message: "Live preview is ready. Hold steady and ensure faces are visible.",
    tone: "success",
  };
};
