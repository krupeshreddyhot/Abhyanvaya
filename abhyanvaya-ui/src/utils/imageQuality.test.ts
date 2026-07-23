import { describe, expect, it } from "vitest";
import {
  estimateFacesFromResolution,
  getImageQualityIndicator,
} from "./imageQuality";
import { getRecognitionReadiness } from "./recognitionReadiness";
import { AIStatus } from "../types/aiWorkflow";

describe("imageQuality", () => {
  it("maps blur scores to enterprise quality labels", () => {
    expect(getImageQualityIndicator(220).label).toBe("Excellent");
    expect(getImageQualityIndicator(140).label).toBe("Good");
    expect(getImageQualityIndicator(90).label).toBe("Acceptable");
    expect(getImageQualityIndicator(50).label).toBe("Retake Recommended");
    expect(getImageQualityIndicator(10).label).toBe("Poor");
    expect(getImageQualityIndicator(null).level).toBe("Unknown");
  });

  it("estimates faces from resolution without AI", () => {
    expect(estimateFacesFromResolution(1920, 1080)).toContain("~");
    expect(estimateFacesFromResolution(null, null)).toBe("Pending");
  });
});

describe("recognitionReadiness", () => {
  it("returns waiting when no images", () => {
    const view = getRecognitionReadiness({
      imageCount: 0,
      status: AIStatus.Ready,
    });
    expect(view.state).toBe("WaitingForImages");
  });

  it("returns complete for awaiting review", () => {
    const view = getRecognitionReadiness({
      imageCount: 2,
      status: AIStatus.AwaitingReview,
    });
    expect(view.state).toBe("RecognitionComplete");
  });

  it("returns failed for failed status", () => {
    const view = getRecognitionReadiness({
      imageCount: 1,
      status: AIStatus.Failed,
    });
    expect(view.state).toBe("RecognitionFailed");
  });
});
