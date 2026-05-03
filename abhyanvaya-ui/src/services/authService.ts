import api from "../api/axios";

export type UniversityOption = {
  code: string;
  name: string;
};

export type LoginResponse = {
  token: string;
  mustChangePassword?: boolean;
};

export type ForgotPasswordResponse = {
  resetToken?: string | null;
  message?: string | null;
  expiresAtUtc?: string | null;
};

export const getUniversities = async () => {
  return await api.get<UniversityOption[]>("/auth/universities");
};

export const login = async (
  universityCode: string,
  collegeCode: string,
  username: string,
  password: string,
) => {
  return await api.post<LoginResponse>("/auth/login", {
    universityCode,
    collegeCode,
    username,
    password,
  });
};

export const superAdminLogin = async (username: string, password: string) => {
  return await api.post<LoginResponse>("/auth/super-admin-login", {
    username,
    password,
  });
};

export const changePassword = async (currentPassword: string, newPassword: string) => {
  return await api.post<LoginResponse>("/auth/change-password", {
    currentPassword,
    newPassword,
  });
};

export const forgotPassword = async (universityCode: string, collegeCode: string, username: string) => {
  return await api.post<ForgotPasswordResponse>("/auth/forgot-password", {
    universityCode,
    collegeCode,
    username,
  });
};

export const resetPasswordWithToken = async (resetToken: string, newPassword: string) => {
  return await api.post<LoginResponse>("/auth/reset-password", {
    resetToken,
    newPassword,
  });
};
