import api from "../api/axios";

export type HeaderInfo = {
  fullName: string;
  shortName: string;
  role: string;
  logoSmPath?: string | null;
  logoMdPath?: string | null;
  logoLgPath?: string | null;
};

export const getHeaderInfo = async () => {
  return await api.get<HeaderInfo>("/ui/header");
};

