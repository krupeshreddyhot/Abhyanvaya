import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const root = resolve(__dirname, "../../..");
const read = (...parts: string[]) => readFileSync(resolve(root, ...parts), "utf8");
const repoDocs = (...parts: string[]) =>
  readFileSync(resolve(root, "..", "..", "docs", ...parts), "utf8");

describe("AI-SCHED-CAP Prompt 8.3 — Publish readiness blocker UX architecture", () => {
  it("documentation exists", () => {
    const doc = repoDocs("AI_SCHED_CAP_PROMPT_8_3_PUBLISH_READINESS_BLOCKER_UX.md");
    expect(doc).toContain("PublishReadinessPanel");
    expect(doc).toContain("SoftWarnings");
    expect(doc).toContain("isBlocking");
    expect(doc).toContain("View entry");
    expect(doc).toMatch(/NOT EXECUTED|Browser E2E/i);
  });

  it("shared panel is used by designer and PublishingPage", () => {
    const panel = read("pages", "setup", "scheduling", "PublishReadinessPanel.tsx");
    const designer = read("pages", "setup", "scheduling", "timetable", "TimetableDesignerPage.tsx");
    const publishing = read("pages", "setup", "scheduling", "governance", "PublishingPage.tsx");
    const soft = read("pages", "setup", "scheduling", "timetable", "SoftWarningsPanel.tsx");

    expect(panel).toContain("getPublishBlockers");
    expect(panel).toContain("isBlocking");
    expect(panel).toContain("View entry");
    expect(panel).toContain("Re-check");
    expect(panel).toContain("aria-labelledby");
    expect(panel).not.toContain("PlacementSizeResolver");
    expect(panel).not.toContain("ConflictEngine");
    expect(panel).not.toContain("createTeachingGroup");

    expect(designer).toContain("PublishReadinessPanel");
    expect(designer).toContain("getTimetablePublishReadiness");
    expect(designer).toContain("parsePublishFailure");
    expect(designer).toContain("openEntryFromPublishFinding");
    expect(designer).toContain("publishTimetable");
    expect(designer).not.toMatch(/if\s*\(.*isReady.*\)\s*\{\s*await publishTimetable/);

    expect(publishing).toContain("PublishReadinessPanel");
    expect(publishing).toContain("getTimetablePublishReadiness");
    expect(publishing).toContain("parsePublishFailure");
    expect(publishing).toContain("entryId=");

    expect(soft).toContain("Informational only");
    expect(soft).not.toContain("PublishReadinessPanel");
    expect(soft).not.toContain("isReady");
  });

  it("no client capacity/conflict/TG mutation in publish UX", () => {
    const panel = read("pages", "setup", "scheduling", "PublishReadinessPanel.tsx");
    const designer = read("pages", "setup", "scheduling", "timetable", "TimetableDesignerPage.tsx");
    for (const src of [panel, designer]) {
      expect(src).not.toContain("ExpectedCapacity *");
      expect(src).not.toContain("resolvedStudentCount >");
      expect(src).not.toContain("createTeachingGroup");
      expect(src).not.toContain("setTimetableSections");
      expect(src).not.toContain("/attendance");
    }
  });

  it("publish button is not gated solely by cached readiness", () => {
    const designer = read("pages", "setup", "scheduling", "timetable", "TimetableDesignerPage.tsx");
    // Publish disabled only by lifecycleBusy, not by !publishReadiness.isReady
    expect(designer).toMatch(/disabled=\{lifecycleBusy\}/);
    expect(designer).not.toMatch(/disabled=\{[^}]*publishReadiness[^}]*isReady/);
    expect(designer).not.toMatch(/disabled=\{[^}]*!.*isReady/);
  });

  it("no automatic publish retry loop", () => {
    const designer = read("pages", "setup", "scheduling", "timetable", "TimetableDesignerPage.tsx");
    const publishing = read("pages", "setup", "scheduling", "governance", "PublishingPage.tsx");
    for (const src of [designer, publishing]) {
      expect(src).not.toMatch(/parsePublishFailure[\s\S]{0,120}publishTimetable\(/);
      expect(src).not.toMatch(/for\s*\(.*\)\s*\{\s*await publishTimetable/);
    }
  });
});
