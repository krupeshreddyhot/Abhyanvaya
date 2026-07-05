import axios from "axios";

const readObjectMessage = (data: Record<string, unknown>): string | null => {
  for (const key of ["detail", "title", "message", "error"] as const) {
    const value = data[key];
    if (typeof value === "string" && value.trim()) {
      return value.trim();
    }
  }

  return null;
};

export const getApiErrorMessage = (error: unknown, fallback = "Request failed."): string => {
  if (axios.isAxiosError(error)) {
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

    if (error.response.status === 400) {
      return "The server rejected this upload. Check image size and dimensions (minimum 640×480).";
    }

    return `Request failed (${error.response.status}).`;
  }

  if (error instanceof Error && error.message.trim()) {
    return error.message;
  }

  return fallback;
};

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
