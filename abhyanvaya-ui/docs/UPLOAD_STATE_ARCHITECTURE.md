# Upload State Architecture (AI11.2.H5)

## Types

`abhyanvaya-ui/src/types/uploadState.ts`

### Enums

| Enum | Values |
|------|--------|
| `UploadValidationStatus` | Idle, Validating, Valid, Invalid |
| `PreviewStatus` | None, Loading, Ready, Failed |
| `UploadStatus` | Idle, Uploading, Completed, Failed, Cancelled |

### Interface

```typescript
interface UploadState {
  selectedFile?: File;
  previewUrl?: string;
  validationStatus: UploadValidationStatus;
  previewStatus: PreviewStatus;
  uploadStatus: UploadStatus;
  progress: number;
  fileName?: string;
  fileSize?: number;
  imageWidth?: number;
  imageHeight?: number;
  errorMessage?: string;
}
```

## Hook

`useUploadState` – manages selection, preview generation, dimensions, and upload lifecycle placeholders.

## Components

| Component | Role |
|-----------|------|
| `ClassroomPhotoUpload` | AI attendance upload area using `UploadState` |
| `MediaUpload` | Shared control; receives mapped props via `uploadStateToMediaUploadProps()` |

## Integration

`AiAttendancePanel` renders `ClassroomPhotoUpload` in the Upload Area section (replacing the upload placeholder).

## Backend

No backend changes in H5. Upload API wiring deferred to AI11.2 upload workflow.

**Status: Complete**
