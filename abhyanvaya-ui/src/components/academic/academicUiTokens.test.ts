import { describe, expect, it } from "vitest";
import { academicStatusChipColor, academicPageShellSx, academicChipSx } from "./academicUiTokens";

describe("AI29.1D Prompt 17 — academic UI tokens", () => {
  it("reuses fluid dashboard maxWidth breakpoints", () => {
    expect(academicPageShellSx.maxWidth).toMatchObject({ md: 1320, lg: 1500, xl: 1750 });
  });

  it("maps status labels to semantic chip colors", () => {
    expect(academicStatusChipColor("Active")).toBe("success");
    expect(academicStatusChipColor("Ready")).toBe("success");
    expect(academicStatusChipColor("Warning")).toBe("warning");
    expect(academicStatusChipColor("Blocked")).toBe("error");
    expect(academicStatusChipColor("Draft")).toBe("warning");
    expect(academicStatusChipColor("Locked")).toBe("info");
  });

  it("keeps compact chip density", () => {
    expect(academicChipSx.height).toBe(22);
  });
});
