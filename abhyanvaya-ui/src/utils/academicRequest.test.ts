import { describe, expect, it } from "vitest";
import { isAbortError, replaceAbortController } from "./academicRequest";

describe("academicRequest helpers", () => {
  it("replaceAbortController aborts the previous controller", () => {
    const first = new AbortController();
    const second = replaceAbortController(first);
    expect(first.signal.aborted).toBe(true);
    expect(second.signal.aborted).toBe(false);
  });

  it("detects abort / cancel errors", () => {
    expect(isAbortError({ name: "AbortError" })).toBe(true);
    expect(isAbortError({ name: "CanceledError", code: "ERR_CANCELED" })).toBe(true);
    expect(isAbortError({ name: "Error" })).toBe(false);
  });
});
