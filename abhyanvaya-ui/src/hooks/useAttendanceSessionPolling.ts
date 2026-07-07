import { useEffect, useRef, useState } from "react";
import { attendanceRecognitionPollingService } from "../services/attendanceRecognitionPollingService";
import type { AiAttendanceState } from "../types/aiAttendanceState";
import type {
  AttendanceSessionStatusResponse,
  RecognitionActivityEntry,
} from "../types/liveSessionStatus";
import {
  buildActivityEntriesFromStatus,
  mapStatusResponseToAiState,
} from "../utils/sessionStatusMapper";

const MAX_ACTIVITY_ITEMS = 100;

export const useAttendanceSessionPolling = (
  sessionId: string | undefined,
  setAiState: React.Dispatch<React.SetStateAction<AiAttendanceState>>,
) => {
  const [activityLog, setActivityLog] = useState<RecognitionActivityEntry[]>([]);
  const previousStatus = useRef<AttendanceSessionStatusResponse | null>(null);

  useEffect(() => {
    if (!sessionId) {
      attendanceRecognitionPollingService.stop();
      return;
    }

    const unsubscribe = attendanceRecognitionPollingService.subscribe((status) => {
      setAiState((current) => mapStatusResponseToAiState(status, current));

      const newEntries = buildActivityEntriesFromStatus(status, previousStatus.current ?? undefined);
      if (newEntries.length > 0) {
        setActivityLog((current) => [...newEntries, ...current].slice(0, MAX_ACTIVITY_ITEMS));
      }

      previousStatus.current = status;
    });

    const unsubscribeError = attendanceRecognitionPollingService.onError(() => {
      /* polling errors are non-fatal; next tick retries */
    });

    attendanceRecognitionPollingService.start(sessionId);

    return () => {
      unsubscribe();
      unsubscribeError();
      attendanceRecognitionPollingService.stop();
    };
  }, [sessionId, setAiState]);

  return { activityLog };
};

export default useAttendanceSessionPolling;
