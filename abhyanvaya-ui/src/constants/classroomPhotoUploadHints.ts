import {
  CLASSROOM_PHOTO_MAX_BYTES,
  CLASSROOM_PHOTO_MIN_HEIGHT,
  CLASSROOM_PHOTO_MIN_WIDTH,
} from "./classroomPhotoConstraints";

export const CLASSROOM_PHOTO_ACCEPT =
  "image/jpeg,image/jpg,image/png,image/webp,.jpg,.jpeg,.png,.webp";

export const CLASSROOM_PHOTO_SUPPORTED_FORMATS = ["JPG", "PNG", "WEBP"] as const;

export const CLASSROOM_PHOTO_MAX_SIZE_LABEL = `${Math.round(
  CLASSROOM_PHOTO_MAX_BYTES / (1024 * 1024),
)} MB`;

export const CLASSROOM_PHOTO_MIN_RESOLUTION_LABEL = `${CLASSROOM_PHOTO_MIN_WIDTH} × ${CLASSROOM_PHOTO_MIN_HEIGHT}`;

/** @deprecated Use structured hints in ClassroomPhotoDropZone */
export const CLASSROOM_PHOTO_FORMAT_HINT = "JPG • PNG • WEBP";
/** @deprecated Use structured hints in ClassroomPhotoDropZone */
export const CLASSROOM_PHOTO_SIZE_HINT = `Maximum ${CLASSROOM_PHOTO_MAX_SIZE_LABEL}`;
/** @deprecated Use structured hints in ClassroomPhotoDropZone */
export const CLASSROOM_PHOTO_RESOLUTION_HINT = `Minimum ${CLASSROOM_PHOTO_MIN_RESOLUTION_LABEL}`;

export const CLASSROOM_PHOTO_DROP_TITLE = "Drag classroom photo here";
export const CLASSROOM_PHOTO_SELECT_LABEL = "Select Classroom Photo";
