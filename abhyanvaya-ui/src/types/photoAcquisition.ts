/** AI22.7A Phase 1 — classroom photo acquisition (presentation contracts). */

export type PhotoAcquisitionMethod = "upload" | "capture" | "capture-multiple";

export type ClassroomPhotoCaptureContext = {
  acquisitionMethod: "Upload" | "CameraCapture" | "CameraMultiCapture";
  captureDevice?: string;
  captureTimestampUtc?: string;
  orientation?: number;
  latitude?: number;
  longitude?: number;
  blurScore?: number;
};

export type CapturedFrame = {
  id: string;
  file: File;
  previewUrl: string;
  width: number;
  height: number;
  blurScore: number | null;
  capturedAt: Date;
};

export const PHOTO_ACQUISITION_METHODS: {
  id: PhotoAcquisitionMethod;
  label: string;
  description: string;
}[] = [
  {
    id: "upload",
    label: "Upload Image",
    description: "Select or drag a classroom photo from your device",
  },
  {
    id: "capture",
    label: "Capture Image",
    description: "Take a single photo with your camera",
  },
  {
    id: "capture-multiple",
    label: "Capture Multiple",
    description: "Capture several frames, then choose one to upload",
  },
];
