import { useCallback, useEffect, useState } from "react";

export type CameraDeviceOption = {
  deviceId: string;
  label: string;
};

export type CameraPermissionState = "unknown" | "granted" | "denied" | "prompt" | "unsupported";

const isMediaDevicesSupported = (): boolean =>
  typeof navigator !== "undefined" &&
  !!navigator.mediaDevices &&
  typeof navigator.mediaDevices.getUserMedia === "function";

export const useCameraDevices = () => {
  const [devices, setDevices] = useState<CameraDeviceOption[]>([]);
  const [permission, setPermission] = useState<CameraPermissionState>("unknown");
  const [error, setError] = useState<string | null>(null);

  const refreshDevices = useCallback(async () => {
    if (!isMediaDevicesSupported()) {
      setPermission("unsupported");
      setDevices([]);
      setError("Camera capture is not supported in this browser.");
      return;
    }

    try {
      const list = await navigator.mediaDevices.enumerateDevices();
      const cameras = list
        .filter((device) => device.kind === "videoinput")
        .map((device, index) => ({
          deviceId: device.deviceId,
          label: device.label?.trim() || `Camera ${index + 1}`,
        }));

      setDevices(cameras);
      setError(null);

      if (cameras.length === 0) {
        setError("No camera was detected on this device.");
      }
    } catch {
      setError("Unable to list cameras.");
    }
  }, []);

  const requestPermission = useCallback(async (): Promise<boolean> => {
    if (!isMediaDevicesSupported()) {
      setPermission("unsupported");
      setError("Camera capture is not supported in this browser.");
      return false;
    }

    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: "environment" },
        audio: false,
      });
      stream.getTracks().forEach((track) => track.stop());
      setPermission("granted");
      setError(null);
      await refreshDevices();
      return true;
    } catch (err) {
      const name = err instanceof DOMException ? err.name : "";
      if (name === "NotAllowedError" || name === "PermissionDeniedError") {
        setPermission("denied");
        setError(
          "Camera permission was denied. Allow camera access in browser settings, then try again.",
        );
      } else if (name === "NotFoundError" || name === "DevicesNotFoundError") {
        setPermission("prompt");
        setError("No camera was found on this device.");
      } else {
        setPermission("prompt");
        setError("Unable to access the camera. Check browser permissions and try again.");
      }
      return false;
    }
  }, [refreshDevices]);

  useEffect(() => {
    if (!isMediaDevicesSupported()) {
      setPermission("unsupported");
      return;
    }

    setPermission("prompt");
    void refreshDevices();

    const onDeviceChange = () => {
      void refreshDevices();
    };

    navigator.mediaDevices.addEventListener?.("devicechange", onDeviceChange);
    return () => {
      navigator.mediaDevices.removeEventListener?.("devicechange", onDeviceChange);
    };
  }, [refreshDevices]);

  return {
    devices,
    permission,
    error,
    supported: isMediaDevicesSupported(),
    refreshDevices,
    requestPermission,
  };
};

export default useCameraDevices;
