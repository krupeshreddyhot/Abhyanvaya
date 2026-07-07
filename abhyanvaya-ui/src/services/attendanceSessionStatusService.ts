import api from "../api/axios";
import type { AttendanceSessionStatusResponse } from "../types/liveSessionStatus";

export const getAttendanceSessionStatus = async (
  sessionId: string,
): Promise<AttendanceSessionStatusResponse> => {
  const response = await api.get<AttendanceSessionStatusResponse>(
    `/attendance-sessions/${sessionId}/status`,
  );
  return response.data;
};
