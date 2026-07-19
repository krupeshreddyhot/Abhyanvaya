import api from "./axios";
import type {
  AvailableCollegeDto,
  ContextDiagnosticsReport,
  PagedCollegesResult,
  RecentCollegeEntry,
  RecentCollegesResult,
  SetCollegeContextRequest,
  TenantContextSnapshot,
} from "../types/tenantContext";

export const getCurrentContext = () => api.get<TenantContextSnapshot>("/context");

export const setCollegeContext = (request: SetCollegeContextRequest) =>
  api.post<TenantContextSnapshot>("/context/college", request);

export const clearContext = () => api.delete<void>("/context");

export const refreshContext = () => api.post<void>("/context/refresh");

export const getRecentColleges = () => api.get<RecentCollegesResult>("/context/recent-colleges");

export const getContextDiagnostics = () => api.get<ContextDiagnosticsReport>("/context/diagnostics");

export const searchAvailableColleges = (params: { search?: string; page?: number; pageSize?: number }) =>
  api.get<PagedCollegesResult>("/context/available-colleges", { params });

export type { AvailableCollegeDto, RecentCollegeEntry, TenantContextSnapshot, RecentCollegesResult };
