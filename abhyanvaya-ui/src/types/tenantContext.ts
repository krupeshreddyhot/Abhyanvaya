export type ContextType = 0 | 1 | 2 | 3 | 4 | 5 | 6;

export type TenantContextSnapshot = {
  userId: number;
  role: string;
  selectedCollegeId: number | null;
  selectedCollegeName: string | null;
  selectedCollegeCode: string | null;
  tenantId: number;
  contextType: ContextType;
  createdUtc: string;
  expiresUtc?: string | null;
  isGlobal: boolean;
  contextSource: string;
};

export type AvailableCollegeDto = {
  id: number;
  tenantId: number;
  name: string;
  code: string;
  shortName: string | null;
  status: string;
  aiEnabled: boolean;
  universityName: string | null;
};

export type RecentCollegeEntry = {
  collegeId: number;
  tenantId: number;
  name: string;
  code: string;
  selectedUtc: string;
  isPinned: boolean;
  isFavorite: boolean;
};

export type RecentCollegesResult = {
  recent: RecentCollegeEntry[];
  popular: AvailableCollegeDto[];
};

export type PagedCollegesResult = {
  items: AvailableCollegeDto[];
  totalCount: number;
  page: number;
  pageSize: number;
};

export type SetCollegeContextRequest = {
  collegeId: number;
};

export type ContextDiagnosticsReport = {
  userId: number;
  role: string;
  jwtTenantId: number;
  operationalContext: TenantContextSnapshot | null;
  persistenceProvider: string;
  contextExists: boolean;
  expiresUtc: string | null;
  remainingTime: string | null;
  isExpired: boolean;
  isValid: boolean;
  validationErrors: string[];
};

export type ContextValidationError = {
  errorCode: string;
  message?: string;
  errors?: string[];
};
