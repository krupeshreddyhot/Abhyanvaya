import { describe, expect, it } from "vitest";
import { buildCourseMasterSavePlan } from "./courseMasterPersistence";

describe("courseMasterPersistence — Prompt 4A no duplicate Program writes", () => {
  it("does not schedule a separate assign-course call when Programs enabled", () => {
    const plan = buildCourseMasterSavePlan({
      editingId: 5,
      code: "254",
      name: "B.Com",
      programId: 10,
      enablePrograms: true,
    });
    expect(plan.callAssignCourseSeparately).toBe(false);
    expect(plan.coursePayload).toMatchObject({
      id: 5,
      code: "254",
      name: "B.Com",
      programId: 10,
    });
  });

  it("omits programId when Programs disabled (legacy Course Master)", () => {
    const plan = buildCourseMasterSavePlan({
      editingId: 0,
      code: "254",
      name: "B.Com",
      programId: 10,
      enablePrograms: false,
    });
    expect(plan.callAssignCourseSeparately).toBe(false);
    expect(plan.coursePayload.programId).toBeUndefined();
    expect(plan.mode).toBe("create");
  });

  it("allows clear / No Program via null programId (AI29.1A policy)", () => {
    const plan = buildCourseMasterSavePlan({
      editingId: 5,
      code: "254",
      name: "B.Com",
      programId: 0,
      enablePrograms: true,
    });
    expect(plan.coursePayload.programId).toBeNull();
    expect(plan.callAssignCourseSeparately).toBe(false);
  });

  it("Prompt 4B — when Programs enabled, update always sends explicit programId (never omit as null)", () => {
    const keep = buildCourseMasterSavePlan({
      editingId: 5,
      code: "BCOM",
      name: "B.Com",
      programId: 15,
      enablePrograms: true,
    });
    expect(Object.prototype.hasOwnProperty.call(keep.coursePayload, "programId")).toBe(true);
    expect(keep.coursePayload.programId).toBe(15);

    const clear = buildCourseMasterSavePlan({
      editingId: 5,
      code: "BCOM",
      name: "B.Com",
      programId: 0,
      enablePrograms: true,
    });
    expect(Object.prototype.hasOwnProperty.call(clear.coursePayload, "programId")).toBe(true);
    expect(clear.coursePayload.programId).toBeNull();
  });
});
