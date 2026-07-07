export const shortenGuid = (guid?: string, length = 8): string => {
  if (!guid) {
    return "—";
  }

  const normalized = guid.replace(/-/g, "").toUpperCase();
  return normalized.slice(0, length);
};

export const normalizeGuidForCopy = (guid: string): string => guid.trim().toLowerCase();
