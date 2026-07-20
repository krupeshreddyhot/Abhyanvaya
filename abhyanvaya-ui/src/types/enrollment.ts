export type EnrollmentDashboardDto = {
  totalStudents: number;
  eligibleStudents: number;
  embedded: number;
  uploadedWithoutEmbedding: number;
  pending: number;
  failed: number;
  processedToday: number;
  runningBatchId: string | null;
  queueLength: number;
  averageDuration: string;
  successRate: number;
};

export type EnrollmentReadinessResult = {
  canStart: boolean;
  eligibleStudents: number;
  runningBatchId: string | null;
  photoProviderReady: boolean;
  storageReady: boolean;
  recognitionReady: boolean;
  workerReady: boolean;
  configurationValid: boolean;
  reasons: string[];
};

export type EnrollmentConfigurationDto = {
  photoProvider: string;
  embeddingEngine: string;
  recognitionEngine: string;
  storageProvider: string;
  retryPolicy: string;
  downloadThreads: number;
  imageFormat: string;
  embeddingDimensions: number;
  photoUrlTemplate: string;
};

export type EnrollmentSystemStatusDto = {
  photoProvider: string;
  photoProviderStatus: string;
  embeddingEngine: string;
  embeddingEngineStatus: string;
  recognitionEngine: string;
  recognitionEngineStatus: string;
  storageProvider: string;
  storageStatus: string;
  workerStatus: string;
};

export type EnrollmentDashboardResponse = {
  dashboard: EnrollmentDashboardDto;
  systemStatus: EnrollmentSystemStatusDto;
  configuration: EnrollmentConfigurationDto;
};

export type EnrollmentFilters = {
  collegeId?: number;
  universityId?: number;
  academicYear?: number;
  courseId?: number;
  groupId?: number;
  batch?: number;
  subjectId?: number;
  search?: string;
  status?: BatchStatus;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDescending?: boolean;
};

export type EnrollmentPreviewRequest = {
  tenantId: number;
  collegeId: number;
  academicYear: number;
  courseId?: number;
  groupId?: number;
  batch?: number;
  subjectId?: number;
  forceReEnrollment?: boolean;
};

export type EnrollmentPreview = {
  eligibleStudentCount: number;
  sampleStudentNumbers: string[];
};

export type CreateEnrollmentBatchApiRequest = {
  universityId: number;
  collegeId: number;
  academicYear: number;
  courseId?: number;
  groupId?: number;
  batch?: number;
  subjectId?: number;
  forceReEnrollment?: boolean;
  photoProvider?: string;
};

export const BatchStatus = {
  Created: 0,
  Running: 1,
  Completed: 2,
  PartiallyFailed: 3,
  Cancelled: 4,
} as const;

export type BatchStatus = (typeof BatchStatus)[keyof typeof BatchStatus];

export type BatchSummary = {
  batchId: string;
  status: BatchStatus;
  totalStudents: number;
  completedCount: number;
  uploadedWithoutEmbedding: number;
  failedCount: number;
  pendingCount: number;
  collegeId: number;
  academicYear: number;
  createdUtc: string;
  completedUtc: string | null;
  progressPercent: number;
  photoProviderName: string | null;
};

export type BatchDetailDto = BatchSummary & {
  universityId: number;
  createdBy: number;
  startedUtc: string | null;
  correlationId: string;
  pipelineVersion: number;
  estimatedRemaining: string | null;
};

export const BatchProgressState = {
  Queued: 0,
  Downloading: 1,
  Validating: 2,
  Embedding: 3,
  Uploading: 4,
  Completed: 5,
  Failed: 6,
  Cancelled: 7,
} as const;

export type BatchProgressState = (typeof BatchProgressState)[keyof typeof BatchProgressState];

export type BatchProgressDto = {
  batchId: string;
  state: BatchProgressState;
  percentage: number;
  estimatedRemaining: string | null;
  queued: number;
  downloading: number;
  validating: number;
  embedding: number;
  completed: number;
  uploadedWithoutEmbedding: number;
  failed: number;
  cancelled: number;
};

export type StudentEnrollmentExplorerItem = {
  itemId: string;
  studentId: number;
  studentNumber: string;
  status: number;
  photoStatus: string;
  validationStatus: string;
  embeddingStatus: string;
  uploadStatus: string;
  recognitionReady: boolean;
  failureReason: string | null;
  retryCount: number;
  downloadUrl: string | null;
  artifactStatus: string;
};

export type PagedResult<T> = {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
};

export type BatchCommandResponse = {
  applied: boolean;
  status: BatchStatus;
  message: string | null;
};

export type CreateBatchResponse = {
  succeeded: boolean;
  batchId: string | null;
  totalStudents: number;
  failureMessage: string | null;
};

export type BatchCreatedEvent = { batchId: string; totalStudents: number };
export type BatchLifecycleEvent = { batchId: string; reason?: string };
