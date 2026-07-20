import { describe, expect, it } from "vitest";
import {
  formatContextAge,
  formatContextRemaining,
  formatContextSelectedLabel,
  formatContextValidUntil,
} from "./contextFormatUtils";

describe("contextFormatUtils", () => {
  it("formats relative age in minutes", () => {
    const created = new Date(Date.now() - 5 * 60_000).toISOString();
    expect(formatContextAge(created)).toBe("5 minutes ago");
    expect(formatContextSelectedLabel(created)).toBe("Selected 5 minutes ago");
  });

  it("formats remaining time", () => {
    const expires = new Date(Date.now() + (7 * 60 + 54) * 60_000).toISOString();
    expect(formatContextRemaining(expires)).toMatch(/7h 5[0-9]m/);
  });

  it("returns dash for missing values", () => {
    expect(formatContextAge(null)).toBe("—");
    expect(formatContextRemaining(undefined)).toBe("—");
    expect(formatContextValidUntil(null)).toBe("—");
  });
});
