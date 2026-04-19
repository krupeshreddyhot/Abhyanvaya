import api from "../api/axios";

export type UniversityDto = {
  id: number;
  code: string;
  name: string;
};

export type TenantCollegeDto = {
  id: number;
  name: string;
  shortName?: string;
  code: string;
  universityId: number;
  universityCode: string;
  universityName: string;
  parentCollegeId?: number | null;
  parentCollegeName?: string | null;
  logoSmPath?: string | null;
  logoMdPath?: string | null;
  logoLgPath?: string | null;
};

export type ParentCollegeOptionDto = {
  id: number;
  name: string;
  code: string;
  shortName?: string | null;
};

export const getAdminUniversities = async () => {
  return await api.get<UniversityDto[]>("/admin/universities");
};

export const createUniversity = async (payload: { code: string; name: string }) => {
  return await api.post<UniversityDto>("/admin/universities", payload);
};

export const getTenantCollege = async () => {
  return await api.get<TenantCollegeDto>("/admin/tenant-college");
};

export const getParentCollegeOptions = async (universityId: number) => {
  return await api.get<ParentCollegeOptionDto[]>("/admin/parent-college-options", {
    params: { universityId },
  });
};

export const updateTenantCollege = async (payload: {
  name: string;
  shortName?: string;
  code: string;
  universityId: number;
  parentCollegeId?: number | null;
}) => {
  return await api.put("/admin/tenant-college", payload);
};

export const uploadTenantCollegeLogo = async (file: File) => {
  const form = new FormData();
  form.append("file", file);
  return await api.post<{ message: string }>("/admin/tenant-college/logo", form);
};
