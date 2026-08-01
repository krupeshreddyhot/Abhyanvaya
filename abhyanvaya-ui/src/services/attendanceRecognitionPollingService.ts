import { getAttendanceSessionStatus } from "./attendanceSessionStatusService";
import type { AttendanceSessionStatusResponse } from "../types/liveSessionStatus";
import { AttendanceSessionStatusCode } from "../utils/attendanceSessionStatus";

const POLL_INTERVAL_MS = 2000;
const TERMINAL_STATUSES = new Set<number>([
  AttendanceSessionStatusCode.Completed,
  AttendanceSessionStatusCode.Approved,
  AttendanceSessionStatusCode.Failed,
  AttendanceSessionStatusCode.Cancelled,
]);

export type PollingCallback = (status: AttendanceSessionStatusResponse) => void;
export type PollingErrorCallback = (error: unknown) => void;

export class AttendanceRecognitionPollingService {
  private sessionId: string | null = null;

  private timerId: ReturnType<typeof setInterval> | null = null;

  private inFlight = false;

  private subscribers = new Set<PollingCallback>();

  private errorSubscribers = new Set<PollingErrorCallback>();

  subscribe(callback: PollingCallback): () => void {
    this.subscribers.add(callback);
    return () => {
      this.subscribers.delete(callback);
    };
  }

  onError(callback: PollingErrorCallback): () => void {
    this.errorSubscribers.add(callback);
    return () => {
      this.errorSubscribers.delete(callback);
    };
  }

  start(sessionId: string): void {
    if (this.sessionId === sessionId && this.timerId != null) {
      return;
    }

    this.beginPolling(sessionId);
  }

  /**
   * Force-restart polling for a session (e.g. after Retry Recognition).
   * Required because polling stops on terminal Failed/Cancelled and will not
   * resume for the same sessionId via {@link start} alone.
   */
  restart(sessionId: string): void {
    this.beginPolling(sessionId);
  }

  stop(): void {
    if (this.timerId != null) {
      clearInterval(this.timerId);
      this.timerId = null;
    }

    this.sessionId = null;
    this.inFlight = false;
  }

  private beginPolling(sessionId: string): void {
    this.stop();
    this.sessionId = sessionId;
    void this.pollOnce();
    this.timerId = setInterval(() => {
      void this.pollOnce();
    }, POLL_INTERVAL_MS);
  }

  dispose(): void {
    this.stop();
    this.subscribers.clear();
    this.errorSubscribers.clear();
  }

  private async pollOnce(): Promise<void> {
    if (!this.sessionId || this.inFlight) {
      return;
    }

    this.inFlight = true;
    try {
      const status = await getAttendanceSessionStatus(this.sessionId);
      this.notify(status);

      if (TERMINAL_STATUSES.has(status.status)) {
        this.stop();
      }
    } catch (error) {
      this.notifyError(error);
    } finally {
      this.inFlight = false;
    }
  }

  private notify(status: AttendanceSessionStatusResponse): void {
    for (const subscriber of this.subscribers) {
      subscriber(status);
    }
  }

  private notifyError(error: unknown): void {
    for (const subscriber of this.errorSubscribers) {
      subscriber(error);
    }
  }
}

export const attendanceRecognitionPollingService = new AttendanceRecognitionPollingService();

export default attendanceRecognitionPollingService;
