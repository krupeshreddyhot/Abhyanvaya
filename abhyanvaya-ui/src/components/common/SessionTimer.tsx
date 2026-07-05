import { Box, Typography } from "@mui/material";
import { useEffect, useState } from "react";
import { AIStatus, type AIStatus as AIStatusType } from "../../types/aiWorkflow";

export type SessionTimerProps = {
  startTime?: Date | null;
  status: AIStatusType;
  elapsedMilliseconds?: number | null;
};

const formatElapsed = (totalSeconds: number): string => {
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;

  return [hours, minutes, seconds].map((part) => String(part).padStart(2, "0")).join(":");
};

const getElapsedSeconds = (startTime: Date, now: Date): number =>
  Math.max(0, Math.floor((now.getTime() - startTime.getTime()) / 1000));

export const SessionTimer = ({ startTime, status, elapsedMilliseconds }: SessionTimerProps) => {
  const [elapsedSeconds, setElapsedSeconds] = useState(0);

  useEffect(() => {
    if (elapsedMilliseconds != null && elapsedMilliseconds >= 0) {
      setElapsedSeconds(Math.floor(elapsedMilliseconds / 1000));
      return;
    }

    if (!startTime) {
      setElapsedSeconds(0);
      return;
    }

    const updateElapsed = () => {
      setElapsedSeconds(getElapsedSeconds(startTime, new Date()));
    };

    updateElapsed();

    if (status !== AIStatus.Processing) {
      return;
    }

    const intervalId = window.setInterval(updateElapsed, 1000);
    return () => window.clearInterval(intervalId);
  }, [elapsedMilliseconds, startTime, status]);

  return (
    <Box>
      <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
        Elapsed Time
      </Typography>
      <Typography variant="h6" component="p" sx={{ fontVariantNumeric: "tabular-nums" }}>
        {formatElapsed(elapsedSeconds)}
      </Typography>
    </Box>
  );
};

export default SessionTimer;
