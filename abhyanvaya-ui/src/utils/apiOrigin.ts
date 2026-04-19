/** API host origin without `/api` suffix (for static files served from the API, e.g. logos). */
export function getApiPublicOrigin(): string {
  const raw = import.meta.env.VITE_API_BASE_URL ?? "https://localhost:7063/api";
  const trimmed = raw.trim().replace(/\/+$/, "");
  return trimmed.replace(/\/?api$/i, "") || trimmed;
}
