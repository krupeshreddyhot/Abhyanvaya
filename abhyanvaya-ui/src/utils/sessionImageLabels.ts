/** AI22.7A-R2 — optional image labels (UI-only, localStorage; no API change). */

const storageKey = (sessionId: string) => `abhyanvaya.sessionImageLabels.${sessionId}`;

export const SUGGESTED_IMAGE_LABELS = [
  "Front Left",
  "Front Right",
  "Back Left",
  "Back Right",
  "Center",
  "Wide Angle",
] as const;

export const loadImageLabels = (sessionId: string | undefined): Record<string, string> => {
  if (!sessionId || typeof localStorage === "undefined") {
    return {};
  }

  try {
    const raw = localStorage.getItem(storageKey(sessionId));
    if (!raw) {
      return {};
    }
    const parsed = JSON.parse(raw) as Record<string, string>;
    return parsed && typeof parsed === "object" ? parsed : {};
  } catch {
    return {};
  }
};

export const saveImageLabel = (
  sessionId: string | undefined,
  imageId: string,
  label: string,
): void => {
  if (!sessionId || typeof localStorage === "undefined") {
    return;
  }

  const current = loadImageLabels(sessionId);
  const trimmed = label.trim();
  if (trimmed) {
    current[imageId] = trimmed.slice(0, 64);
  } else {
    delete current[imageId];
  }

  localStorage.setItem(storageKey(sessionId), JSON.stringify(current));
};

export const clearImageLabels = (sessionId: string | undefined): void => {
  if (!sessionId || typeof localStorage === "undefined") {
    return;
  }
  localStorage.removeItem(storageKey(sessionId));
};
