import api from "../api/axios";

export type CreateTenantUserPayload = {
  username: string;
  password: string;
  role: "Admin" | "Faculty";
  /** Required for Faculty — maps login to directory row and subject assignments. */
  staffId?: number | null;
  courseId?: number | null;
  groupId?: number | null;
  applicationRoleIds: number[];
};

export const createTenantUser = (payload: CreateTenantUserPayload) =>
  api.post<{ id: number }>("/tenant-users", payload);

export const adminResetUserPassword = (userId: number, newPassword: string) =>
  api.post(`/tenant-users/${userId}/reset-password`, { newPassword });

/** Link or unlink a Faculty user to a staff profile (JWT updates on next login). */
export const linkUserStaff = (userId: number, staffId: number | null) =>
  api.put(`/tenant-users/${userId}/staff`, { staffId });
