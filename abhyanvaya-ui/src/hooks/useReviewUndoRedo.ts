import { useCallback, useRef, useState } from "react";
import type { AttendanceRecognitionReviewDto } from "../services/attendanceRecognitionService";
import {
  RecognitionReviewAction,
  type RecognitionReviewActionValue,
} from "../services/attendanceRecognitionService";

export type ReviewUndoEntry = {
  recognitionId: string;
  action: RecognitionReviewActionValue;
  previous: AttendanceRecognitionReviewDto;
  redoStudentId?: number;
};

/**
 * AI22.7A Phase 4.5 — client-side undo/redo stack for teacher review actions.
 * Undo uses Reset; redo re-applies the prior action (no workflow redesign).
 */
export function useReviewUndoRedo() {
  const undoRef = useRef<ReviewUndoEntry[]>([]);
  const redoRef = useRef<ReviewUndoEntry[]>([]);
  const [, setTick] = useState(0);
  const bump = useCallback(() => setTick((n) => n + 1), []);

  const pushAction = useCallback(
    (entry: ReviewUndoEntry) => {
      undoRef.current = [...undoRef.current.slice(-49), entry];
      redoRef.current = [];
      bump();
    },
    [bump],
  );

  const popUndo = useCallback((): ReviewUndoEntry | null => {
    const entry = undoRef.current[undoRef.current.length - 1] ?? null;
    if (!entry) {
      return null;
    }
    undoRef.current = undoRef.current.slice(0, -1);
    bump();
    return entry;
  }, [bump]);

  const commitUndo = useCallback(
    (entry: ReviewUndoEntry) => {
      redoRef.current = [...redoRef.current, entry];
      bump();
    },
    [bump],
  );

  const popRedo = useCallback((): ReviewUndoEntry | null => {
    const entry = redoRef.current[redoRef.current.length - 1] ?? null;
    if (!entry) {
      return null;
    }
    redoRef.current = redoRef.current.slice(0, -1);
    bump();
    return entry;
  }, [bump]);

  const commitRedo = useCallback(
    (entry: ReviewUndoEntry) => {
      undoRef.current = [...undoRef.current, entry];
      bump();
    },
    [bump],
  );

  return {
    canUndo: undoRef.current.length > 0,
    canRedo: redoRef.current.length > 0,
    undoCount: undoRef.current.length,
    redoCount: redoRef.current.length,
    pushAction,
    popUndo,
    commitUndo,
    popRedo,
    commitRedo,
    resetAction: RecognitionReviewAction.Reset,
  };
}
