import { describe, expect, it } from "vitest";
import { AcademicPermissionAccess } from "./academicPermissionAccess";
import { PermissionKeys } from "./permissionKeys";

describe("AI29.1D Prompt 16A — operational context permission catalog", () => {
  it("allows Attendance without requiring Program.View", () => {
    const keys = AcademicPermissionAccess.operationalContext.viewAny;
    expect(keys).toContain(PermissionKeys.AttendanceView);
    expect(keys).toContain(PermissionKeys.AttendanceManage);
    expect(keys).toContain(PermissionKeys.ProgramView);
    expect(keys).not.toContain(PermissionKeys.ProgramCreate);
    expect(keys).not.toContain(PermissionKeys.ProgramEdit);
    expect(keys).not.toContain(PermissionKeys.ProgramDelete);
    expect(keys).not.toContain(PermissionKeys.ProgramManage);
  });

  it("covers Section, Timetable, and Allocation consumers", () => {
    const keys = AcademicPermissionAccess.operationalContext.viewAny;
    expect(keys).toContain(PermissionKeys.SectionView);
    expect(keys).toContain(PermissionKeys.SchedulingTimetableView);
    expect(keys).toContain(PermissionKeys.AllocationRun);
    expect(keys).toContain(PermissionKeys.AllocationOperationsView);
  });
});
