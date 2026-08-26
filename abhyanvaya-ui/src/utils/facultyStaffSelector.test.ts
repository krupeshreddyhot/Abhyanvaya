import { describe, expect, it } from "vitest";
import {
  facultyIdForAssign,
  formatFacultyDisplayName,
  formatFacultyOptionLabel,
  formatFacultySelectionSummary,
  formatFacultyStaffId,
} from "./facultyStaffSelector";

describe("AI29.1D.15A Prompt 6 — Faculty staff selector", () => {
  const faculty = {
    id: 42,
    firstName: "Ada",
    lastName: "Lovelace",
    staffCode: "EMP-0042",
  };

  it("formats human-readable name and staff id", () => {
    expect(formatFacultyDisplayName(faculty)).toBe("Ada Lovelace");
    expect(formatFacultyStaffId(faculty)).toBe("EMP-0042");
    expect(formatFacultyOptionLabel(faculty)).toBe("Ada Lovelace · EMP-0042");
  });

  it("falls back to Staff #id when staffCode missing", () => {
    expect(formatFacultyStaffId({ id: 7, staffCode: null })).toBe("Staff #7");
    expect(formatFacultyStaffId({ id: 7, staffCode: "  " })).toBe("Staff #7");
  });

  it("selection summary exposes Name + Staff ID for UX confirmation", () => {
    expect(formatFacultySelectionSummary(faculty)).toEqual({
      name: "Ada Lovelace",
      staffId: "EMP-0042",
    });
  });

  it("assign id is authoritative Staff id — never derived from display name", () => {
    expect(facultyIdForAssign(faculty)).toBe(42);
    expect(facultyIdForAssign(null)).toBeNull();
    expect(facultyIdForAssign({ id: 0, firstName: "X", lastName: "Y" })).toBeNull();
  });
});
