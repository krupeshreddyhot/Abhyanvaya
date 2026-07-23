import { describe, expect, it } from "vitest";
import {
  CLASSROOM_PHOTO_BLUR_WARN_THRESHOLD,
  estimateBlurScore,
} from "./classroomImageProcessing";

const createImageDataLike = (width: number, height: number): ImageData =>
  ({
    data: new Uint8ClampedArray(width * height * 4),
    width,
    height,
    colorSpace: "srgb",
  }) as ImageData;

describe("estimateBlurScore", () => {
  it("returns 0 for tiny images", () => {
    const data = createImageDataLike(2, 2);
    expect(estimateBlurScore(data)).toBe(0);
  });

  it("scores a high-contrast checkerboard higher than a flat image", () => {
    const sharp = createImageDataLike(8, 8);
    for (let y = 0; y < 8; y += 1) {
      for (let x = 0; x < 8; x += 1) {
        const i = (y * 8 + x) * 4;
        const v = (x + y) % 2 === 0 ? 255 : 0;
        sharp.data[i] = v;
        sharp.data[i + 1] = v;
        sharp.data[i + 2] = v;
        sharp.data[i + 3] = 255;
      }
    }

    const flat = createImageDataLike(8, 8);
    for (let i = 0; i < flat.data.length; i += 4) {
      flat.data[i] = 128;
      flat.data[i + 1] = 128;
      flat.data[i + 2] = 128;
      flat.data[i + 3] = 255;
    }

    expect(estimateBlurScore(sharp)).toBeGreaterThan(estimateBlurScore(flat));
    expect(CLASSROOM_PHOTO_BLUR_WARN_THRESHOLD).toBeGreaterThan(0);
  });
});
