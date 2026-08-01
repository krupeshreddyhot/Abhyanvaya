/** AI22.7A Phase 5 — UI-only review workspace preferences (localStorage). */

const PREFIX = "abhyanvaya.reviewWorkspace.";

export type ReviewWorkspacePrefs = {
  fullscreen: boolean;
  heatMapEnabled: boolean;
  heatMapOpacity: number;
  miniMapVisible: boolean;
  smartQueueOnlyPending: boolean;
  lastImageSequenceBySession: Record<string, number>;
};

const DEFAULTS: ReviewWorkspacePrefs = {
  fullscreen: false,
  heatMapEnabled: false,
  heatMapOpacity: 0.35,
  miniMapVisible: true,
  smartQueueOnlyPending: true,
  lastImageSequenceBySession: {},
};

function readAll(): ReviewWorkspacePrefs {
  if (typeof localStorage === "undefined") {
    return { ...DEFAULTS, lastImageSequenceBySession: {} };
  }
  try {
    const raw = localStorage.getItem(`${PREFIX}prefs`);
    if (!raw) {
      return { ...DEFAULTS, lastImageSequenceBySession: {} };
    }
    const parsed = JSON.parse(raw) as Partial<ReviewWorkspacePrefs>;
    return {
      ...DEFAULTS,
      ...parsed,
      lastImageSequenceBySession: parsed.lastImageSequenceBySession ?? {},
      heatMapOpacity:
        typeof parsed.heatMapOpacity === "number"
          ? Math.min(1, Math.max(0.1, parsed.heatMapOpacity))
          : DEFAULTS.heatMapOpacity,
    };
  } catch {
    return { ...DEFAULTS, lastImageSequenceBySession: {} };
  }
}

function writeAll(prefs: ReviewWorkspacePrefs): void {
  if (typeof localStorage === "undefined") {
    return;
  }
  localStorage.setItem(`${PREFIX}prefs`, JSON.stringify(prefs));
}

export function loadReviewWorkspacePrefs(): ReviewWorkspacePrefs {
  return readAll();
}

export function saveReviewWorkspacePrefs(patch: Partial<ReviewWorkspacePrefs>): ReviewWorkspacePrefs {
  const next = { ...readAll(), ...patch };
  writeAll(next);
  return next;
}

export function getLastImageSequence(sessionId: string | undefined): number | null {
  if (!sessionId) {
    return null;
  }
  const value = readAll().lastImageSequenceBySession[sessionId];
  return typeof value === "number" && value >= 1 ? value : null;
}

export function setLastImageSequence(sessionId: string | undefined, sequence: number): void {
  if (!sessionId || sequence < 1) {
    return;
  }
  const current = readAll();
  saveReviewWorkspacePrefs({
    lastImageSequenceBySession: {
      ...current.lastImageSequenceBySession,
      [sessionId]: sequence,
    },
  });
}
