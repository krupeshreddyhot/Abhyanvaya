import {
  CLASSROOM_PHOTO_MAX_BYTES,
  CLASSROOM_PHOTO_MIN_HEIGHT,
  CLASSROOM_PHOTO_MIN_WIDTH,
} from "../constants/classroomPhotoConstraints";

export type PreparedClassroomUpload = {
  file: File;
  width: number;
  height: number;
};

const readImageDimensions = (file: Blob): Promise<{ width: number; height: number }> =>
  new Promise((resolve, reject) => {
    const url = URL.createObjectURL(file);
    const image = new Image();

    image.onload = () => {
      const width = image.naturalWidth;
      const height = image.naturalHeight;
      URL.revokeObjectURL(url);
      if (!width || !height) {
        reject(new Error("Unable to read image dimensions."));
        return;
      }
      resolve({ width, height });
    };

    image.onerror = () => {
      URL.revokeObjectURL(url);
      reject(new Error("Unable to decode the selected image. Use JPG, PNG, or WebP."));
    };

    image.src = url;
  });

/**
 * Lightweight prepare step for Upload Image.
 * Validates dimensions and uploads the original file when possible
 * (avoids canvas/createImageBitmap failures that blocked uploads).
 */
export const prepareClassroomUploadFile = async (file: File): Promise<PreparedClassroomUpload> => {
  if (!file || file.size <= 0) {
    throw new Error("Choose a non-empty image file.");
  }

  if (file.size > CLASSROOM_PHOTO_MAX_BYTES) {
    throw new Error(
      `Classroom photo must be ${Math.round(CLASSROOM_PHOTO_MAX_BYTES / (1024 * 1024))} MB or smaller.`,
    );
  }

  const { width, height } = await readImageDimensions(file);

  if (width < CLASSROOM_PHOTO_MIN_WIDTH || height < CLASSROOM_PHOTO_MIN_HEIGHT) {
    throw new Error(
      `Classroom photo must be at least ${CLASSROOM_PHOTO_MIN_WIDTH}×${CLASSROOM_PHOTO_MIN_HEIGHT} pixels. Your image is ${width}×${height}px.`,
    );
  }

  return { file, width, height };
};
