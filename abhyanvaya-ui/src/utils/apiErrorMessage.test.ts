import axios from "axios";
import { describe, expect, it } from "vitest";
import {
  getApiErrorMessage,
  getHttpStatus,
  isForbiddenError,
  isUnauthorizedError,
} from "./apiErrorMessage";

const axiosError = (status: number, data?: unknown) => {
  const err = new axios.AxiosError("fail");
  err.response = {
    status,
    data,
    statusText: "",
    headers: {},
    config: { headers: new axios.AxiosHeaders() },
  };
  return err;
};

describe("AI29.1D Prompt 18 — API 401/403 handling", () => {
  it("prefers server body over status fallback", () => {
    expect(getApiErrorMessage(axiosError(403, "Section out of scope"))).toBe("Section out of scope");
    expect(getApiErrorMessage(axiosError(403, { message: "Denied by policy" }))).toBe("Denied by policy");
  });

  it("maps empty 401/403 to clear UX copy", () => {
    expect(getApiErrorMessage(axiosError(401))).toMatch(/session|authenticated|Sign in/i);
    expect(getApiErrorMessage(axiosError(403))).toMatch(/not authorized/i);
  });

  it("supports domain forbiddenFallback without inventing auth rules", () => {
    expect(
      getApiErrorMessage(axiosError(403), "Request failed.", {
        forbiddenFallback: "You are not assigned to this subject.",
      }),
    ).toBe("You are not assigned to this subject.");
  });

  it("exposes status helpers", () => {
    expect(getHttpStatus(axiosError(401))).toBe(401);
    expect(isUnauthorizedError(axiosError(401))).toBe(true);
    expect(isForbiddenError(axiosError(403))).toBe(true);
  });
});
