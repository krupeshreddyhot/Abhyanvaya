import { useCallback, useEffect, useRef, useState } from "react";

export type UseCameraStreamOptions = {
  deviceId?: string | null;
  enabled: boolean;
};

export const useCameraStream = ({ deviceId, enabled }: UseCameraStreamOptions) => {
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const stop = useCallback(() => {
    streamRef.current?.getTracks().forEach((track) => track.stop());
    streamRef.current = null;
    if (videoRef.current) {
      videoRef.current.srcObject = null;
    }
    setReady(false);
  }, []);

  const start = useCallback(async () => {
    if (!enabled) {
      stop();
      return;
    }

    if (!navigator.mediaDevices?.getUserMedia) {
      setError("Camera is not supported in this browser.");
      return;
    }

    stop();
    setError(null);

    const constraints: MediaStreamConstraints = {
      audio: false,
      video: deviceId
        ? { deviceId: { exact: deviceId }, width: { ideal: 1920 }, height: { ideal: 1080 } }
        : {
            facingMode: { ideal: "environment" },
            width: { ideal: 1920 },
            height: { ideal: 1080 },
          },
    };

    try {
      const stream = await navigator.mediaDevices.getUserMedia(constraints);
      streamRef.current = stream;
      if (videoRef.current) {
        videoRef.current.srcObject = stream;
        await videoRef.current.play();
        setReady(true);
      }
    } catch (err) {
      const name = err instanceof DOMException ? err.name : "";
      if (name === "OverconstrainedError" && deviceId) {
        try {
          const fallback = await navigator.mediaDevices.getUserMedia({
            audio: false,
            video: true,
          });
          streamRef.current = fallback;
          if (videoRef.current) {
            videoRef.current.srcObject = fallback;
            await videoRef.current.play();
            setReady(true);
            setError(null);
            return;
          }
        } catch {
          // fall through
        }
      }

      setReady(false);
      setError(
        name === "NotAllowedError"
          ? "Camera permission was denied."
          : "Unable to start the camera preview.",
      );
    }
  }, [deviceId, enabled, stop]);

  useEffect(() => {
    void start();
    return () => {
      stop();
    };
  }, [start, stop]);

  const setVideoElement = useCallback(
    (element: HTMLVideoElement | null) => {
      videoRef.current = element;
      if (element && streamRef.current) {
        element.srcObject = streamRef.current;
        void element.play().then(() => setReady(true)).catch(() => undefined);
      }
    },
    [],
  );

  return {
    videoRef: setVideoElement,
    mediaVideoRef: videoRef,
    ready,
    error,
    restart: start,
    stop,
  };
};

export default useCameraStream;
