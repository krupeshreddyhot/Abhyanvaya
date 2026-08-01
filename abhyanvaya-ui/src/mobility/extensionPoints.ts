/**
 * AI22.7C Phase 2 extension points (stubs only).
 * Do not implement offline / PWA / background sync in Phase 1.
 */

export type MobilityPhase2Capability =
  | "offline-read-cache"
  | "offline-sync-queue"
  | "pwa-install"
  | "background-sync";

/** Feature flags reserved for Phase 2 — all disabled in Phase 1. */
export const MOBILITY_PHASE2_EXTENSIONS = {
  offlineMode: false,
  pwaInstall: false,
  backgroundSync: false,
} as const;

export function isPhase2CapabilityEnabled(_capability: MobilityPhase2Capability): boolean {
  return false;
}
