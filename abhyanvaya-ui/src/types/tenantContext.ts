export const ContextType = {
  Global: 0,
  University: 1,
  College: 2,
  Campus: 3,
  Department: 4,
  Course: 5,
  Section: 6,
} as const;

export type ContextType = (typeof ContextType)[keyof typeof ContextType];

export type TenantContextSnapshot = {
  userId: number;
  role: string;
  selectedCollegeId: number | null;
  selectedCollegeName: string | null;
  selectedCollegeCode: string | null;
  tenantId: number;
  contextType: ContextType;
  createdUtc: string;
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

export type PagedCollegesResult = {
  items: AvailableCollegeDto[];
  totalCount: number;
  page: number;
  pageSize: number;
};

export type SetCollegeContextRequest = {
  collegeId: number;
};

export type ContextValidationError = {
  errorCode: string;
  message?: string;
  errors?: string[];
};
