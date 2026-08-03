import { useCallback, useRef, useState } from "react";

export type TimetableHistoryEntry = {
  label: string;
  undo: () => Promise<void>;
  redo: () => Promise<void>;
};

const MAX_HISTORY = 50;

/** Client-side undo/redo stack for timetable entry operations (max ~50). */
export function useTimetableHistory() {
  const undoRef = useRef<TimetableHistoryEntry[]>([]);
  const redoRef = useRef<TimetableHistoryEntry[]>([]);
  const [, setTick] = useState(0);
  const bump = useCallback(() => setTick((n) => n + 1), []);

  const push = useCallback(
    (entry: TimetableHistoryEntry) => {
      undoRef.current = [...undoRef.current.slice(-(MAX_HISTORY - 1)), entry];
      redoRef.current = [];
      bump();
    },
    [bump],
  );

  const undo = useCallback(async (): Promise<TimetableHistoryEntry | null> => {
    const entry = undoRef.current[undoRef.current.length - 1] ?? null;
    if (!entry) return null;
    undoRef.current = undoRef.current.slice(0, -1);
    await entry.undo();
    redoRef.current = [...redoRef.current, entry];
    bump();
    return entry;
  }, [bump]);

  const redo = useCallback(async (): Promise<TimetableHistoryEntry | null> => {
    const entry = redoRef.current[redoRef.current.length - 1] ?? null;
    if (!entry) return null;
    redoRef.current = redoRef.current.slice(0, -1);
    await entry.redo();
    undoRef.current = [...undoRef.current, entry];
    bump();
    return entry;
  }, [bump]);

  const reset = useCallback(() => {
    undoRef.current = [];
    redoRef.current = [];
    bump();
  }, [bump]);

  return {
    canUndo: undoRef.current.length > 0,
    canRedo: redoRef.current.length > 0,
    push,
    undo,
    redo,
    reset,
  };
}
