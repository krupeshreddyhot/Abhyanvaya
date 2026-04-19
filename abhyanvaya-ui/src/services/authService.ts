import api from "../api/axios";

export type UniversityOption = {
  code: string;
  name: string;
};

export const getUniversities = async () => {
  return await api.get<UniversityOption[]>("/auth/universities");
};

export const login = async (
  universityCode: string,
  collegeCode: string,
  username: string,
  password: string
) => {
  return await api.post("/auth/login", {
    universityCode,
    collegeCode,
    username,
    password,
  });
};
