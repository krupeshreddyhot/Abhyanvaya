import api from "./axios";
import type {
  AvailableCollegeDto,
  PagedCollegesResult,
  SetCollegeContextRequest,
  TenantContextSnapshot,
} from "../types/tenantContext";

export const getCurrentContext = () => api.get<TenantContextSnapshot>("/context");

export const setCollegeContext = (request: SetCollegeContextRequest) =>
  api.post<TenantContextSnapshot>("/context/college", request);

export const clearContext = () => api.delete<void>("/context");

export const searchAvailableColleges = (params: { search?: string; page?: number; pageSize?: number }) =>
  api.get<PagedCollegesResult>("/context/available-colleges", { params });

export type { AvailableCollegeDto, TenantContextSnapshot };
