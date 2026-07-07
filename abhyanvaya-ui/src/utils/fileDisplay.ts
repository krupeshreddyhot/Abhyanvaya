import { formatBytes } from "./uploadProgress";

export const formatImageMimeLabel = (file?: File): string => {
  if (!file) {
    return "—";
  }

  const mime = (file.type ?? "").toLowerCase();
  if (mime.includes("jpeg") || mime.includes("jpg")) {
    return "JPEG";
  }

  if (mime.includes("png")) {
    return "PNG";
  }

  if (mime.includes("webp")) {
    return "WEBP";
  }

  const extension = file.name.split(".").pop()?.toUpperCase();
  return extension ?? "Image";
};

export const formatResolution = (width?: number, height?: number): string => {
  if (!width || !height) {
    return "—";
  }

  return `${width.toLocaleString()} × ${height.toLocaleString()}`;
};

export const formatUploadedTimestamp = (uploadedAt?: Date | null): string => {
  if (!uploadedAt) {
    return "—";
  }

  const now = new Date();
  const isToday =
    uploadedAt.getFullYear() === now.getFullYear() &&
    uploadedAt.getMonth() === now.getMonth() &&
    uploadedAt.getDate() === now.getDate();

  const time = uploadedAt.toLocaleTimeString(undefined, {
    hour: "numeric",
    minute: "2-digit",
  });

  if (isToday) {
    return `Today ${time}`;
  }

  return uploadedAt.toLocaleString(undefined, {
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  });
};

export const formatFileSizeLabel = (bytes?: number): string =>
  bytes != null && bytes > 0 ? formatBytes(bytes) : "—";
