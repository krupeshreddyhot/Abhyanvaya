/** Human-readable relative time helpers for operational context UX (AI20.2 Phase 4). */

export const formatContextAge = (createdUtc?: string | null): string => {
  if (!createdUtc) return "—";
  const created = new Date(createdUtc);
  const diffMs = Date.now() - created.getTime();
  const minutes = Math.floor(diffMs / 60000);
  if (minutes < 1) return "just now";
  if (minutes < 60) return `${minutes} minute${minutes === 1 ? "" : "s"} ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours} hour${hours === 1 ? "" : "s"} ago`;
  const days = Math.floor(hours / 24);
  return `${days} day${days === 1 ? "" : "s"} ago`;
};

export const formatContextRemaining = (expiresUtc?: string | null): string => {
  if (!expiresUtc) return "—";
  const remainingMs = new Date(expiresUtc).getTime() - Date.now();
  if (remainingMs <= 0) return "expired";
  const totalMinutes = Math.floor(remainingMs / 60000);
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  if (hours > 0) return `${hours}h ${minutes}m`;
  return `${minutes}m`;
};

export const formatContextValidUntil = (expiresUtc?: string | null): string => {
  if (!expiresUtc) return "—";
  return new Date(expiresUtc).toLocaleString(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  });
};

export const formatContextSelectedLabel = (createdUtc?: string | null): string => {
  if (!createdUtc) return "Context not established";
  return `Selected ${formatContextAge(createdUtc)}`;
};
