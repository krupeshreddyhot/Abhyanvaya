import api from "../api/axios";

const base = "/tenant-rbac";

export type PermissionCatalogItem = {
  id: number;
  key: string;
  resource: string;
  action: string;
};

export type ApplicationRoleListItem = {
  id: number;
  name: string;
  code: string;
  description: string | null;
  permissionCount: number;
  assignedUserCount: number;
};

export type ApplicationRoleDetail = {
  id: number;
  name: string;
  code: string;
  description: string | null;
  permissionIds: number[];
};

export type TenantUserRbacRow = {
  id: number;
  username: string;
  enumRole: string;
  /** Faculty directory link from API; absent when not Faculty or legacy account. */
  staffId?: number | null;
  applicationRoleIds: number[];
};

export const listPermissionCatalog = () => api.get<PermissionCatalogItem[]>(`${base}/permissions`);

export const listApplicationRoles = () => api.get<ApplicationRoleListItem[]>(`${base}/roles`);

export const getApplicationRole = (id: number) => api.get<ApplicationRoleDetail>(`${base}/roles/${id}`);

export const createApplicationRole = (body: { name: string; code: string; description?: string | null }) =>
  api.post<{ id: number }>(`${base}/roles`, body);

export const updateApplicationRole = (id: number, body: { name: string; description?: string | null }) =>
  api.put(`${base}/roles/${id}`, body);

export const setRolePermissions = (id: number, permissionIds: number[]) =>
  api.put(`${base}/roles/${id}/permissions`, { permissionIds });

export const deleteApplicationRole = (id: number) => api.delete(`${base}/roles/${id}`);

export const listTenantUsersRbac = () => api.get<TenantUserRbacRow[]>(`${base}/users`);

export const setUserApplicationRoles = (userId: number, applicationRoleIds: number[]) =>
  api.put(`${base}/users/${userId}/application-roles`, { applicationRoleIds });
