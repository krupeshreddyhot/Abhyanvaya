import { useMemo } from "react";
import {
  CLASSROOM_PHOTO_BLUR_WARN_THRESHOLD,
} from "../utils/classroomImageProcessing";

/**
 * Soft blur-detection hook (AI22.7A). Does not block upload — presentation warning only.
 */
export const useBlurDetection = (blurScore: number | null | undefined) => {
  return useMemo(() => {
    if (blurScore == null || Number.isNaN(blurScore)) {
      return {
        blurScore: null as number | null,
        isBlurry: false,
        warning: null as string | null,
      };
    }

    const isBlurry = blurScore < CLASSROOM_PHOTO_BLUR_WARN_THRESHOLD;
    return {
      blurScore,
      isBlurry,
      warning: isBlurry
        ? "This image may be blurry. Consider retaking for better face recognition."
        : null,
    };
  }, [blurScore]);
};

export default useBlurDetection;
