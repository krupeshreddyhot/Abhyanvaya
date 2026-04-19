import type { AxiosResponse } from "axios";
import api from "../api/axios";

/** Subject-wise summary for one student */
export type StudentAttendanceRow = {
  subject: string;
  total: number;
  present: number;
  percentage: number;
};

/** Per-student summary for one subject */
export type SubjectAttendanceRow = {
  student: string;
  total: number;
  present: number;
  percentage: number;
};

/** Map API rows whether JSON uses camelCase or PascalCase property names. */
function mapStudentAttendanceRow(raw: unknown): StudentAttendanceRow {
  const r = raw as Record<string, unknown>;
  const pctVal = r.percentage ?? r.Percentage;
  return {
    subject: String(r.subject ?? r.Subject ?? ""),
    total: Number(r.total ?? r.Total ?? 0),
    present: Number(r.present ?? r.Present ?? 0),
    percentage: Number(pctVal ?? 0),
  };
}

function mapSubjectAttendanceRow(raw: unknown): SubjectAttendanceRow {
  const r = raw as Record<string, unknown>;
  const pctVal = r.percentage ?? r.Percentage;
  return {
    student: String(r.student ?? r.Student ?? ""),
    total: Number(r.total ?? r.Total ?? 0),
    present: Number(r.present ?? r.Present ?? 0),
    percentage: Number(pctVal ?? 0),
  };
}

export const getStudentReport = async (
  studentNumber: string,
): Promise<AxiosResponse<StudentAttendanceRow[]>> => {
  const res = await api.get<unknown[]>("/reports/student", {
    params: { studentNumber: studentNumber.trim() },
  });
  return { ...res, data: res.data.map(mapStudentAttendanceRow) };
};

export const getMonthlyStudentReport = async (
  studentNumber: string,
  month: number,
  year: number,
): Promise<AxiosResponse<StudentAttendanceRow[]>> => {
  const res = await api.get<unknown[]>("/reports/monthly", {
    params: { studentNumber: studentNumber.trim(), month, year },
  });
  return { ...res, data: res.data.map(mapStudentAttendanceRow) };
};

export const getSubjectReport = async (subjectId: number): Promise<AxiosResponse<SubjectAttendanceRow[]>> => {
  const res = await api.get<unknown[]>("/reports/subject", {
    params: { subjectId },
  });
  return { ...res, data: res.data.map(mapSubjectAttendanceRow) };
};
