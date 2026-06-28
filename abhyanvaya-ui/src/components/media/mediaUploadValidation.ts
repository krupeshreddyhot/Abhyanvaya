export const MEDIA_UPLOAD_MAX_BYTES = 5 * 1024 * 1024;

export const MEDIA_UPLOAD_ACCEPT =
  "image/jpeg,image/jpg,image/png,image/webp,.jpg,.jpeg,.png,.webp";

const ALLOWED_EXTENSIONS = new Set([".jpg", ".jpeg", ".png", ".webp"]);
const ALLOWED_MIME_TYPES = new Set(["image/jpeg", "image/jpg", "image/pjpeg", "image/png", "image/webp"]);

const BLOCKED_EXTENSIONS = new Set([".bmp", ".gif", ".svg", ".exe", ".zip"]);

export type MediaUploadValidationResult = {
  ok: boolean;
  error?: string;
};

const blockedExtensionMessage = (ext: string): string => {
  switch (ext.toLowerCase()) {
    case ".bmp":
      return "BMP images are not allowed. Use JPG, JPEG, PNG, or WebP.";
    case ".gif":
      return "GIF images are not allowed. Use JPG, JPEG, PNG, or WebP.";
    case ".svg":
      return "SVG files are not allowed. Use JPG, JPEG, PNG, or WebP.";
    case ".exe":
      return "Executable files are not allowed.";
    case ".zip":
      return "ZIP archives are not allowed.";
    default:
      return "This file type is not allowed.";
  }
};

const getExtension = (fileName: string): string => {
  const base = fileName.replace(/\\/g, "/").split("/").pop() ?? fileName;
  const idx = base.lastIndexOf(".");
  return idx >= 0 ? base.slice(idx).toLowerCase() : "";
};

const hasPathTraversal = (fileName: string): boolean =>
  fileName.includes("..") || fileName.includes("/") || fileName.includes("\\");

export const validateMediaUploadFile = (
  file: File,
  maxBytes: number = MEDIA_UPLOAD_MAX_BYTES
): MediaUploadValidationResult => {
  if (!file || file.size <= 0) {
    return { ok: false, error: "Choose a non-empty image file." };
  }

  if (file.size > maxBytes) {
    return { ok: false, error: `File is too large. Maximum size is ${Math.round(maxBytes / (1024 * 1024))} MB.` };
  }

  if (hasPathTraversal(file.name)) {
    return { ok: false, error: "Invalid file name." };
  }

  const extension = getExtension(file.name);
  if (extension && BLOCKED_EXTENSIONS.has(extension)) {
    return { ok: false, error: blockedExtensionMessage(extension) };
  }

  const mime = (file.type ?? "").trim().toLowerCase();
  if (mime === "image/gif" || mime === "image/svg+xml" || mime.includes("zip") || mime.includes("bmp")) {
    return { ok: false, error: "This file type is not allowed. Use JPG, JPEG, PNG, or WebP." };
  }

  const extensionAllowed = extension ? ALLOWED_EXTENSIONS.has(extension) : false;
  const mimeAllowed = mime ? ALLOWED_MIME_TYPES.has(mime) : false;

  if (!extensionAllowed && !mimeAllowed) {
    return { ok: false, error: "Allowed file types: JPG, JPEG, PNG, or WebP." };
  }

  if (extension && !extensionAllowed) {
    return { ok: false, error: "File extension is not allowed. Use JPG, JPEG, PNG, or WebP." };
  }

  if (mime && mime !== "application/octet-stream" && !mimeAllowed) {
    return { ok: false, error: "File type is not allowed. Use JPG, JPEG, PNG, or WebP." };
  }

  return { ok: true };
};
