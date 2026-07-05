export const UPLOAD_PROGRESS_MILESTONES = [0, 10, 25, 40, 60, 80, 100] as const;

export const mapUploadProgressToMilestone = (loaded: number, total: number): number => {
  if (total <= 0) {
    return 0;
  }

  const percent = (loaded / total) * 100;
  let milestone: number = UPLOAD_PROGRESS_MILESTONES[0];

  for (const step of UPLOAD_PROGRESS_MILESTONES) {
    if (percent >= step) {
      milestone = step;
    }
  }

  return milestone;
};

export const formatBytes = (bytes: number): string => {
  if (bytes <= 0) {
    return "0 B";
  }

  const units = ["B", "KB", "MB", "GB"];
  const exponent = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1);
  const value = bytes / 1024 ** exponent;
  return `${value.toFixed(exponent === 0 ? 0 : 1)} ${units[exponent]}`;
};

export const sleep = (ms: number): Promise<void> =>
  new Promise((resolve) => {
    window.setTimeout(resolve, ms);
  });
