import { getApiPublicOrigin } from "./apiOrigin";

/** Absolute URL for a path such as `/branding/{guid}/md.webp?v=…`. */
export function brandingAssetUrl(path: string | null | undefined): string | null {
  if (!path) return null;
  return `${getApiPublicOrigin()}${path.startsWith("/") ? path : `/${path}`}`;
}
