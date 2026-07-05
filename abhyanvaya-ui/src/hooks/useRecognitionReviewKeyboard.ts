import { useEffect } from "react";
import {
  RecognitionReviewAction,
  type RecognitionReviewActionValue,
} from "../services/attendanceRecognitionService";

type UseRecognitionReviewKeyboardOptions = {
  focusedId: string | null;
  disabled: boolean;
  onAction: (recognitionId: string, action: RecognitionReviewActionValue) => void;
};

function isEditableTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) {
    return false;
  }

  const tag = target.tagName;
  return tag === "INPUT" || tag === "TEXTAREA" || target.isContentEditable;
}

export function useRecognitionReviewKeyboard({
  focusedId,
  disabled,
  onAction,
}: UseRecognitionReviewKeyboardOptions) {
  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (disabled || !focusedId || isEditableTarget(event.target)) {
        return;
      }

      const key = event.key.toLowerCase();
      let action: RecognitionReviewActionValue | null = null;

      if (key === "a") {
        action = RecognitionReviewAction.Approve;
      } else if (key === "r") {
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
  }, [disabled, focusedId, onAction]);
}
