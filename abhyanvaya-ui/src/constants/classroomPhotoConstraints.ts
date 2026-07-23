/** Must match Abhyanvaya.Infrastructure.Validation.ClassroomImageValidator */
export const CLASSROOM_PHOTO_MIN_WIDTH = 640;
export const CLASSROOM_PHOTO_MIN_HEIGHT = 480;
export const CLASSROOM_PHOTO_MAX_BYTES = 15 * 1024 * 1024;

/** Must match ClassroomPhotoCollectionLimits.MaxImagesPerSession (AI22.7A Phase 2). */
export const CLASSROOM_PHOTO_MAX_IMAGES_PER_SESSION = 10;

export const validateClassroomPhotoDimensions = (
  width: number,
  height: number,
): string | null => {
  if (width < CLASSROOM_PHOTO_MIN_WIDTH || height < CLASSROOM_PHOTO_MIN_HEIGHT) {
    return `Classroom photo must be at least ${CLASSROOM_PHOTO_MIN_WIDTH}×${CLASSROOM_PHOTO_MIN_HEIGHT} pixels. Your image is ${width}×${height}px.`;
  }

  return null;
};
