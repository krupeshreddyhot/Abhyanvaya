import axios from "axios";

const readObjectMessage = (data: Record<string, unknown>): string | null => {
  for (const key of [
    "detail",
    "title",
    "message",
    "Message",
    "error",
    "failureMessage",
    "FailureMessage",
  ] as const) {
    const value = data[key];
    if (typeof value === "string" && value.trim()) {
      return value.trim();
    }
  }

  return null;
};

export const getHttpStatus = (error: unknown): number | undefined => {
  if (axios.isAxiosError(error)) {
    return error.response?.status;
  }
  return undefined;
};

export const isUnauthorizedError = (error: unknown): boolean => getHttpStatus(error) === 401;
export const isForbiddenError = (error: unknown): boolean => getHttpStatus(error) === 403;

export type ApiErrorMessageOptions = {
  /**
   * Domain-specific copy when the server returns 403 without a body.
   * Does not invent authorization rules — only presentation after the API rejects.
   */
  forbiddenFallback?: string;
  /** Domain-specific copy when the server returns 401 without a body. */
  unauthorizedFallback?: string;
};

/**
 * Prefer server-provided message body. Map 401/403 to clear UX when the body is empty.
 * UI permission checks never replace this — the API remains authoritative.
 */
export const getApiErrorMessage = (
  error: unknown,
  fallback = "Request failed.",
  options?: ApiErrorMessageOptions,
): string => {
  if (axios.isAxiosError(error)) {
    const status = error.response?.status;
    const data = error.response?.data;

    if (typeof data === "string" && data.trim()) {
      return data.trim();
    }

    if (data && typeof data === "object") {
      const message = readObjectMessage(data as Record<string, unknown>);
      if (message) {
        return message;
      }
    }

    if (!error.response) {
      return "Network error. Check your connection and try again.";
    }

    if (status === 401) {
      return (
        options?.unauthorizedFallback ??
        "Your session has expired or is not authenticated. Sign in again and retry."
      );
    }

    if (status === 403) {
      return (
        options?.forbiddenFallback ??
        "You are not authorized to perform this action. If you believe this is wrong, ask an administrator to review your permissions."
      );
    }

    return fallback;
  }

  if (error instanceof Error && error.message.trim()) {
    return error.message;
  }

  return fallback;
};

export const getEnrollmentApiErrorMessage = (error: unknown, fallback = "Enrollment request failed."): string =>
  getApiErrorMessage(error, fallback);

/** Upload endpoints use a photo-specific fallback when the API returns no message body. */
export const getUploadApiErrorMessage = (error: unknown): string =>
  getApiErrorMessage(
    error,
    "The server rejected this upload. Check image size and dimensions (minimum 640×480).",
  );

export const isRetryableUploadError = (error: unknown): boolean => {
  if (axios.isAxiosError(error)) {
    if (!error.response) {
      return true;
    }

    const status = error.response.status;
    return status >= 500 || status === 408 || status === 429;
  }

  return false;
};
