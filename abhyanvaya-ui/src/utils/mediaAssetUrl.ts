import { getApiPublicOrigin } from "./apiOrigin";

/** Absolute URL for a media path such as `/media/students/1/42/thumbnail.webp?v=…`. */
export function mediaAssetUrl(path: string | null | undefined): string | null {
  if (!path) return null;
  if (/^https?:\/\//i.test(path)) return path;
  return `${getApiPublicOrigin()}${path.startsWith("/") ? path : `/${path}`}`;
}
