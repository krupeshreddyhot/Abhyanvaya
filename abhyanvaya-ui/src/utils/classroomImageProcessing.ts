import {
  CLASSROOM_PHOTO_MAX_BYTES,
  CLASSROOM_PHOTO_MIN_HEIGHT,
  CLASSROOM_PHOTO_MIN_WIDTH,
} from "../constants/classroomPhotoConstraints";

export type ProcessedClassroomImage = {
  file: File;
  width: number;
  height: number;
  blurScore: number | null;
  orientationApplied: boolean;
};

const JPEG_QUALITY = 0.85;
const MAX_EDGE_PX = 2560;

/** Soft blur threshold — below this Laplacian variance, warn (does not block upload). */
export const CLASSROOM_PHOTO_BLUR_WARN_THRESHOLD = 80;

const loadImageElement = (source: Blob | string): Promise<HTMLImageElement> =>
  new Promise((resolve, reject) => {
    const image = new Image();
    const url = typeof source === "string" ? source : URL.createObjectURL(source);

    image.onload = () => {
      if (typeof source !== "string") {
        URL.revokeObjectURL(url);
      }
      resolve(image);
    };

    image.onerror = () => {
      if (typeof source !== "string") {
        URL.revokeObjectURL(url);
      }
      reject(new Error("Unable to decode image for processing."));
    };

    image.src = url;
  });

/**
 * Draws source into a canvas with automatic orientation via createImageBitmap when available.
 * Compresses to JPEG under classroom constraints.
 */
export const processClassroomImageFile = async (
  input: Blob,
  fileName = "classroom-capture.jpg",
): Promise<ProcessedClassroomImage> => {
  let bitmap: ImageBitmap | null = null;
  let width: number;
  let height: number;
  let orientationApplied = false;

  try {
    if (typeof createImageBitmap === "function") {
      bitmap = await createImageBitmap(input, {
        imageOrientation: "from-image",
      } as ImageBitmapOptions);
      width = bitmap.width;
      height = bitmap.height;
      orientationApplied = true;
    } else {
      const image = await loadImageElement(input);
      width = image.naturalWidth;
      height = image.naturalHeight;
    }
  } catch {
    const image = await loadImageElement(input);
    width = image.naturalWidth;
    height = image.naturalHeight;
    bitmap = null;
  }

  if (width < CLASSROOM_PHOTO_MIN_WIDTH || height < CLASSROOM_PHOTO_MIN_HEIGHT) {
    bitmap?.close();
    throw new Error(
      `Classroom photo must be at least ${CLASSROOM_PHOTO_MIN_WIDTH}×${CLASSROOM_PHOTO_MIN_HEIGHT} pixels. Your image is ${width}×${height}px.`,
    );
  }

  const scale = Math.min(1, MAX_EDGE_PX / Math.max(width, height));
  const targetWidth = Math.max(1, Math.round(width * scale));
  const targetHeight = Math.max(1, Math.round(height * scale));

  const canvas = document.createElement("canvas");
  canvas.width = targetWidth;
  canvas.height = targetHeight;
  const ctx = canvas.getContext("2d", { willReadFrequently: true });
  if (!ctx) {
    bitmap?.close();
    throw new Error("Unable to process classroom image.");
  }

  if (bitmap) {
    ctx.drawImage(bitmap, 0, 0, targetWidth, targetHeight);
    bitmap.close();
  } else {
    const image = await loadImageElement(input);
    ctx.drawImage(image, 0, 0, targetWidth, targetHeight);
  }

  const blurScore = estimateBlurScore(ctx.getImageData(0, 0, targetWidth, targetHeight));

  const blob = await new Promise<Blob>((resolve, reject) => {
    canvas.toBlob(
      (result) => {
        if (!result) {
          reject(new Error("Image compression failed."));
          return;
        }
        resolve(result);
      },
      "image/jpeg",
      JPEG_QUALITY,
    );
  });

  if (blob.size > CLASSROOM_PHOTO_MAX_BYTES) {
    throw new Error("Classroom photo must be 15 MB or smaller after compression.");
  }

  const safeName = fileName.toLowerCase().endsWith(".jpg") || fileName.toLowerCase().endsWith(".jpeg")
    ? fileName
    : `${fileName.replace(/\.[^.]+$/, "") || "classroom-capture"}.jpg`;

  return {
    file: new File([blob], safeName, { type: "image/jpeg", lastModified: Date.now() }),
    width: targetWidth,
    height: targetHeight,
    blurScore,
    orientationApplied,
  };
};

/**
 * Laplacian variance blur estimate on grayscale luminance.
 * Higher score ≈ sharper. Hook for soft quality warnings (AI22.7A).
 */
export const estimateBlurScore = (imageData: ImageData): number => {
  const { data, width, height } = imageData;
  if (width < 3 || height < 3) {
    return 0;
  }

  const gray = new Float32Array(width * height);
  for (let i = 0, p = 0; i < data.length; i += 4, p += 1) {
    gray[p] = 0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2];
  }

  let sum = 0;
  let sumSq = 0;
  let count = 0;

  for (let y = 1; y < height - 1; y += 1) {
    for (let x = 1; x < width - 1; x += 1) {
      const i = y * width + x;
      const lap =
        -gray[i - width] - gray[i - 1] + 4 * gray[i] - gray[i + 1] - gray[i + width];
      sum += lap;
      sumSq += lap * lap;
      count += 1;
    }
  }

  if (count === 0) {
    return 0;
  }

  const mean = sum / count;
  const variance = sumSq / count - mean * mean;
  return Math.max(0, Math.round(variance * 100) / 100);
};

export const captureFrameFromVideo = async (
  video: HTMLVideoElement,
  fileName = `classroom-capture-${Date.now()}.jpg`,
): Promise<ProcessedClassroomImage> => {
  if (!video.videoWidth || !video.videoHeight) {
    throw new Error("Camera preview is not ready yet.");
  }

  const canvas = document.createElement("canvas");
  canvas.width = video.videoWidth;
  canvas.height = video.videoHeight;
  const ctx = canvas.getContext("2d");
  if (!ctx) {
    throw new Error("Unable to capture camera frame.");
  }

  ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

  const blob = await new Promise<Blob>((resolve, reject) => {
    canvas.toBlob(
      (result) => {
        if (!result) {
          reject(new Error("Camera capture failed."));
          return;
        }
        resolve(result);
      },
      "image/jpeg",
      JPEG_QUALITY,
    );
  });

  return processClassroomImageFile(blob, fileName);
};
