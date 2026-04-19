import api from "../api/axios";

export type DashboardOverviewDto = {
  totalStudents: number;
  totalSubjects: number;
  totalAttendance: number;
  totalPresent: number;
  overallPercentage: number;
  todayPresent: number;
  todayAbsent: number;
};

export const getDashboardOverview = async () => api.get<DashboardOverviewDto>("/dashboard/overview");

