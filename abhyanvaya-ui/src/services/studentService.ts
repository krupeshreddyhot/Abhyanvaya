import api from "../api/axios";

export type UploadStudentsResultDto = {
  imported: number;
  skipped: number;
  errors: string[];
};

export const uploadStudentsExcel = async (file: File) => {
  const formData = new FormData();
  formData.append("file", file);
  return await api.post<UploadStudentsResultDto>("/student/upload", formData);
};
