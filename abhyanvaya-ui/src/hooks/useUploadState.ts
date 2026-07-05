import { useCallback, useEffect, useState } from "react";
import {
  CLASSROOM_PHOTO_MAX_BYTES,
  validateClassroomPhotoDimensions,
} from "../constants/classroomPhotoConstraints";
import {
  createInitialUploadState,
  PreviewStatus,
  UploadStatus,
  UploadValidationStatus,
  type UploadState,
} from "../types/uploadState";

const readImageDimensions = (file: File): Promise<{ width: number; height: number }> =>
  new Promise((resolve, reject) => {
    const url = URL.createObjectURL(file);
    const image = new Image();

    image.onload = () => {
      resolve({ width: image.naturalWidth, height: image.naturalHeight });
      URL.revokeObjectURL(url);
    };

    image.onerror = () => {
      URL.revokeObjectURL(url);
      reject(new Error("Unable to preview the selected image."));
    };

    image.src = url;
  });

export const useUploadState = (initialState?: UploadState) => {
  const [uploadState, setUploadState] = useState<UploadState>(initialState ?? createInitialUploadState());

  useEffect(() => {
    return () => {
      if (uploadState.previewUrl?.startsWith("blob:")) {
        URL.revokeObjectURL(uploadState.previewUrl);
      }
    };
  }, [uploadState.previewUrl]);

  const resetUploadState = useCallback(() => {
    setUploadState((current) => {
      if (current.previewUrl?.startsWith("blob:")) {
        URL.revokeObjectURL(current.previewUrl);
      }

      return createInitialUploadState();
    });
  }, []);

  const selectFile = useCallback(async (file: File) => {
    setUploadState((current) => {
      if (current.previewUrl?.startsWith("blob:")) {
        URL.revokeObjectURL(current.previewUrl);
      }

      return {
        ...createInitialUploadState(),
        selectedFile: file,
        fileName: file.name,
        fileSize: file.size,
        bytesTotal: file.size,
        bytesUploaded: 0,
        validationStatus: UploadValidationStatus.Validating,
        previewStatus: PreviewStatus.Loading,
      };
    });

    try {
      if (file.size > CLASSROOM_PHOTO_MAX_BYTES) {
        throw new Error("Classroom photo must be 15 MB or smaller.");
      }

      const previewUrl = URL.createObjectURL(file);
      const dimensions = await readImageDimensions(file);
      const dimensionError = validateClassroomPhotoDimensions(dimensions.width, dimensions.height);
      if (dimensionError) {
        URL.revokeObjectURL(previewUrl);
        throw new Error(dimensionError);
      }

      setUploadState((current) => ({
        ...current,
        selectedFile: file,
        previewUrl,
        fileName: file.name,
        fileSize: file.size,
        bytesTotal: file.size,
        imageWidth: dimensions.width,
        imageHeight: dimensions.height,
        validationStatus: UploadValidationStatus.Valid,
        previewStatus: PreviewStatus.Ready,
        errorMessage: undefined,
      }));
    } catch (error) {
      setUploadState((current) => ({
        ...current,
        validationStatus: UploadValidationStatus.Invalid,
        previewStatus: PreviewStatus.Failed,
        errorMessage: error instanceof Error ? error.message : "Unable to preview the selected image.",
      }));
      throw error;
    }
  }, []);

  const setUploadProgress = useCallback((progress: number) => {
    setUploadState((current) => ({
      ...current,
      uploadStatus: UploadStatus.Uploading,
      progress,
    }));
  }, []);

  const setBytesProgress = useCallback((loaded: number, total: number) => {
    setUploadState((current) => ({
      ...current,
      bytesUploaded: loaded,
      bytesTotal: total,
    }));
  }, []);

  const setRetrying = useCallback((attempt: number) => {
    setUploadState((current) => ({
      ...current,
      uploadStatus: UploadStatus.Retrying,
      retryAttempt: attempt + 1,
      errorMessage: `Retrying upload... Attempt ${attempt + 1} of ${current.maxRetries ?? 3}`,
    }));
  }, []);

  const markUploadCompleted = useCallback((previewUrl?: string) => {
    setUploadState((current) => ({
      ...current,
      uploadStatus: UploadStatus.Completed,
      progress: 100,
      bytesUploaded: current.bytesTotal ?? current.fileSize,
      previewUrl: previewUrl ?? current.previewUrl,
      errorMessage: undefined,
      retryAttempt: 0,
      uploadedAt: new Date(),
    }));
  }, []);

  const markUploadFailed = useCallback((message: string) => {
    setUploadState((current) => ({
      ...current,
      uploadStatus: UploadStatus.Failed,
      errorMessage: message,
    }));
  }, []);

  const markUploadCancelled = useCallback(() => {
    setUploadState((current) => ({
      ...current,
      uploadStatus: UploadStatus.Cancelled,
    }));
  }, []);

  return {
    uploadState,
    selectFile,
    resetUploadState,
    setUploadProgress,
    setBytesProgress,
    setRetrying,
    markUploadCompleted,
    markUploadFailed,
    markUploadCancelled,
  };
};

export default useUploadState;
