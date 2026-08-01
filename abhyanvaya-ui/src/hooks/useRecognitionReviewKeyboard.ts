import { useEffect } from "react";
import {
  RecognitionReviewAction,
  type RecognitionReviewActionValue,
} from "../services/attendanceRecognitionService";

type UseRecognitionReviewKeyboardOptions = {
  focusedId: string | null;
  disabled: boolean;
  onAction: (recognitionId: string, action: RecognitionReviewActionValue) => void;
  onNext?: () => void;
  onPrevious?: () => void;
  onNextImage?: () => void;
  onPreviousImage?: () => void;
  onManualMatch?: (recognitionId: string) => void;
  onUndo?: () => void;
  onRedo?: () => void;
  onToggleFullscreen?: () => void;
  onToggleHeatMap?: () => void;
  onToggleMiniMap?: () => void;
  onToggleHelp?: () => void;
  onExitFullscreen?: () => void;
};

function isEditableTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) {
    return false;
  }

  const tag = target.tagName;
  return tag === "INPUT" || tag === "TEXTAREA" || target.isContentEditable;
}

/**
 * AI22.7A Phase 4.4 + Phase 5.7 — keyboard productivity mode.
 * Space = next face (pan uses Alt+drag / middle-drag in the viewer).
 */
export function useRecognitionReviewKeyboard({
  focusedId,
  disabled,
  onAction,
  onNext,
  onPrevious,
  onNextImage,
  onPreviousImage,
  onManualMatch,
  onUndo,
  onRedo,
  onToggleFullscreen,
  onToggleHeatMap,
  onToggleMiniMap,
  onToggleHelp,
  onExitFullscreen,
}: UseRecognitionReviewKeyboardOptions) {
  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (disabled || isEditableTarget(event.target)) {
        return;
      }

      const key = event.key.toLowerCase();
      const isMod = event.ctrlKey || event.metaKey;

      if (event.key === "Escape") {
        event.preventDefault();
        onExitFullscreen?.();
        return;
      }

      if (event.key === "?" || (event.shiftKey && key === "/")) {
        event.preventDefault();
        onToggleHelp?.();
        return;
      }

      if (isMod && key === "z") {
        event.preventDefault();
        onUndo?.();
        return;
      }

      if (isMod && (key === "y" || (event.shiftKey && key === "z"))) {
        event.preventDefault();
        onRedo?.();
        return;
      }

      if (event.key === "Tab") {
        event.preventDefault();
        if (event.shiftKey) {
          onPreviousImage?.();
        } else {
          onNextImage?.();
        }
        return;
      }

      if (event.code === "Space") {
        event.preventDefault();
        if (event.shiftKey) {
          onPrevious?.();
        } else {
          onNext?.();
        }
        return;
      }

      if (event.key === "Enter" && focusedId) {
        event.preventDefault();
        onAction(focusedId, RecognitionReviewAction.Approve);
        return;
      }

      if (key === "f") {
        event.preventDefault();
        onToggleFullscreen?.();
        return;
      }

      if (key === "h") {
        event.preventDefault();
        onToggleHeatMap?.();
        return;
      }

      if (key === "m" && !isMod) {
        event.preventDefault();
        onToggleMiniMap?.();
        return;
      }

      if (isMod && key === "m" && focusedId) {
        event.preventDefault();
        onManualMatch?.(focusedId);
        return;
      }

      if (key === "n") {
        event.preventDefault();
        onNext?.();
        return;
      }

      if (key === "p") {
        event.preventDefault();
        onPrevious?.();
        return;
      }

      if (!focusedId) {
        return;
      }

      let action: RecognitionReviewActionValue | null = null;

      if (key === "a") {
        action = RecognitionReviewAction.Approve;
      } else if (key === "r" || key === "delete" || key === "backspace") {
        action = RecognitionReviewAction.Reject;
      } else if (key === "i") {
        action = RecognitionReviewAction.Ignore;
      }

      if (action == null) {
        return;
      }

      event.preventDefault();
      onAction(focusedId, action);
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [
    disabled,
    focusedId,
    onAction,
    onExitFullscreen,
    onManualMatch,
    onNext,
    onNextImage,
    onPrevious,
    onPreviousImage,
    onRedo,
    onToggleFullscreen,
    onToggleHeatMap,
    onToggleHelp,
    onToggleMiniMap,
    onUndo,
  ]);
}
