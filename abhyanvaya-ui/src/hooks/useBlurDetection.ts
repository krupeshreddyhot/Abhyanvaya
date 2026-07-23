/**
 * Soft blur-detection hook (AI22.7A / R1). Does not block upload — presentation warning only.
 */
import { useMemo } from "react";
import { CLASSROOM_PHOTO_BLUR_WARN_THRESHOLD } from "../utils/classroomImageProcessing";
import { getImageQualityIndicator } from "../utils/imageQuality";

export const useBlurDetection = (blurScore: number | null | undefined) => {
  return useMemo(() => {
    if (blurScore == null || Number.isNaN(blurScore)) {
      return {
        blurScore: null as number | null,
        isBlurry: false,
        warning: null as string | null,
        quality: getImageQualityIndicator(null),
      };
    }

    const quality = getImageQualityIndicator(blurScore);
    const isBlurry = blurScore < CLASSROOM_PHOTO_BLUR_WARN_THRESHOLD;
    return {
      blurScore,
      isBlurry,
      quality,
      warning: isBlurry
        ? `${quality.stars} ${quality.label} — consider retaking for better face recognition.`
        : null,
    };
  }, [blurScore]);
};

export default useBlurDetection;
