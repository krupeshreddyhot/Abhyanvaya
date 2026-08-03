import { getApiPublicOrigin } from "./apiOrigin";

/**
 * Absolute URL for a media path such as `/media/students/1/42/thumbnail.webp?v=…`.
 * In local Vite dev, keep same-origin `/media/...` (or `/api/media/...`) so the Vite proxy
 * fetches from the API — direct https://localhost:7063 often breaks &lt;img&gt; (untrusted cert).
 */
export function mediaAssetUrl(path: string | null | undefined): string | null {
  if (!path) return null;
  if (/^https?:\/\//i.test(path)) return path;

  const normalized = path.startsWith("/") ? path : `/${path}`;

  // Prefer /api/media so media rides the same Vite /api proxy hop as JSON calls.
  if (import.meta.env.DEV && normalized.startsWith("/media/")) {
    return `/api${normalized}`;
  }

  if (import.meta.env.DEV && normalized.startsWith("/api/media/")) {
    return normalized;
  }

  return `${getApiPublicOrigin()}${normalized}`;
}
