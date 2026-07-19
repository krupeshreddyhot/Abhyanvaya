import * as signalR from "@microsoft/signalr";
import api from "./axios";
import type {
  BatchCommandResponse,
  BatchDetailDto,
  BatchProgressDto,
  CreateBatchResponse,
  CreateEnrollmentBatchApiRequest,
  EnrollmentDashboardResponse,
  EnrollmentFilters,
  EnrollmentPreview,
  EnrollmentPreviewRequest,
  EnrollmentReadinessResult,
  PagedResult,
  BatchSummary,
  StudentEnrollmentExplorerItem,
} from "../types/enrollment";

const resolveHubBaseUrl = (): string => {
  const apiBase = import.meta.env.VITE_API_BASE_URL || "https://localhost:7063/api";
  return apiBase.replace(/\/api\/?$/, "");
};

export class EnrollmentApiClient {
  private hubConnection: signalR.HubConnection | null = null;

  getDashboard = (collegeId?: number) =>
    api.get<EnrollmentDashboardResponse>("/enrollment/dashboard", { params: { collegeId } });

  getReadiness = (params: {
    collegeId: number;
    academicYear: number;
    courseId?: number;
    groupId?: number;
    batch?: number;
    subjectId?: number;
    forceReEnrollment?: boolean;
  }) => api.get<EnrollmentReadinessResult>("/enrollment/readiness", { params });

  getHistory = (filters: EnrollmentFilters) =>
    api.get<PagedResult<BatchSummary>>("/enrollment/history", { params: filters });

  getBatches = (filters: EnrollmentFilters) =>
    api.get<PagedResult<BatchSummary>>("/enrollment/batches", { params: filters });

  getBatch = (batchId: string) => api.get<BatchDetailDto>(`/enrollment/batches/${batchId}`);

  getBatchProgress = (batchId: string) =>
    api.get<BatchProgressDto>(`/enrollment/batches/${batchId}/progress`);

  getBatchStudents = (batchId: string, filters: EnrollmentFilters) =>
    api.get<PagedResult<StudentEnrollmentExplorerItem>>(`/enrollment/batches/${batchId}/students`, {
      params: filters,
    });

  previewBatch = (request: EnrollmentPreviewRequest) =>
    api.post<EnrollmentPreview>("/enrollment/preview", request);

  createBatch = (request: CreateEnrollmentBatchApiRequest) =>
    api.post<CreateBatchResponse>("/enrollment/batches", request);

  cancelBatch = (batchId: string) =>
    api.post<BatchCommandResponse>(`/enrollment/batches/${batchId}/cancel`);

  retryBatch = (batchId: string) =>
    api.post<BatchCommandResponse>(`/enrollment/batches/${batchId}/retry`);

  async connectSignalR(
    token: string,
    handlers: {
      onBatchCreated?: (payload: { batchId: string; totalStudents: number }) => void;
      onBatchStarted?: (payload: { batchId: string }) => void;
      onBatchProgress?: (progress: BatchProgressDto) => void;
      onBatchCompleted?: (payload: { batchId: string }) => void;
      onBatchFailed?: (payload: { batchId: string; reason: string }) => void;
      onBatchCancelled?: (payload: { batchId: string }) => void;
    },
  ): Promise<void> {
    await this.disconnectSignalR();

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${resolveHubBaseUrl()}/hubs/enrollment`, {
        accessTokenFactory: () => token,
        withCredentials: true,
      })
      .withAutomaticReconnect()
      .build();

    if (handlers.onBatchCreated) {
      this.hubConnection.on("BatchCreated", handlers.onBatchCreated);
    }
    if (handlers.onBatchStarted) {
      this.hubConnection.on("BatchStarted", handlers.onBatchStarted);
    }
    if (handlers.onBatchProgress) {
      this.hubConnection.on("BatchProgress", handlers.onBatchProgress);
    }
    if (handlers.onBatchCompleted) {
      this.hubConnection.on("BatchCompleted", handlers.onBatchCompleted);
    }
    if (handlers.onBatchFailed) {
      this.hubConnection.on("BatchFailed", handlers.onBatchFailed);
    }
    if (handlers.onBatchCancelled) {
      this.hubConnection.on("BatchCancelled", handlers.onBatchCancelled);
    }

    await this.hubConnection.start();
  }

  async subscribeTenant(): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      await this.hubConnection.invoke("SubscribeTenant");
    }
  }

  async subscribeBatch(batchId: string): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      await this.hubConnection.invoke("SubscribeBatch", batchId);
    }
  }

  async disconnectSignalR(): Promise<void> {
    if (this.hubConnection) {
      await this.hubConnection.stop();
      this.hubConnection = null;
    }
  }
}

export const enrollmentApiClient = new EnrollmentApiClient();
