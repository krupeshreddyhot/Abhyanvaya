import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const root = resolve(__dirname, "../../..");
const read = (...parts: string[]) => readFileSync(resolve(root, ...parts), "utf8");
const repoDocs = (...parts: string[]) =>
  readFileSync(resolve(root, "..", "..", "docs", ...parts), "utf8");

/**
 * AI-SCHED-CAP Prompt 8.2 — Architecture guards for publish readiness client contract.
 */
describe("AI-SCHED-CAP Prompt 8.2 — publish readiness client architecture", () => {
  it("documentation exists", () => {
    const doc = repoDocs("AI_SCHED_CAP_PROMPT_8_2_PUBLISH_READINESS_UI_CLIENT_CONTRACT.md");
    expect(doc).toContain("isReady");
    expect(doc).toContain("isBlocking");
    expect(doc).toContain("getTimetablePublishReadiness");
    expect(doc).toContain("SoftWarnings");
    expect(doc).toContain("parsePublishFailure");
  });

  it("API client lives in schedulingService and uses existing api abstraction", () => {
    const service = read("services", "schedulingService.ts");
    expect(service).toContain("TimetablePublishReadinessResultDto");
    expect(service).toContain("PublishReadinessFindingDto");
    expect(service).toContain("isReady");
    expect(service).toContain("isBlocking");
    expect(service).toContain("getTimetablePublishReadiness");
    expect(service).toContain("`/scheduling/timetables/${id}/publish-readiness`");
    expect(service).toMatch(
      /publishTimetable\s*=\s*\([^)]*\)\s*=>\s*[\s\S]*?`\/scheduling\/timetables\/\$\{id\}\/publish`/,
    );
  });

  it("shared parser exists and trusts server isBlocking", () => {
    const util = read("pages", "setup", "scheduling", "publishReadiness.ts");
    expect(util).toContain("parsePublishFailure");
    expect(util).toContain("normalizePublishReadiness");
    expect(util).toContain("getPublishBlockers");
    expect(util).toContain("isBlocking");
    expect(util).not.toMatch(/isBlocking\s*=\s*severity/);
    expect(util).not.toMatch(/isBlocking\s*=\s*.*ROOM_CAPACITY/);
    expect(util).not.toContain("PlacementSizeResolver");
    expect(util).not.toContain("ConflictEngine");
    expect(util).not.toContain("fetch(");
  });

  it("designer and PublishingPage parse publish failures without SoftWarnings reuse or auto-retry", () => {
    const designer = read("pages", "setup", "scheduling", "timetable", "TimetableDesignerPage.tsx");
    const publishing = read("pages", "setup", "scheduling", "governance", "PublishingPage.tsx");
    const soft = read("pages", "setup", "scheduling", "timetable", "SoftWarningsPanel.tsx");

    expect(designer).toContain("parsePublishFailure");
    expect(designer).toContain("publishReadiness");
    expect(designer).not.toMatch(/for\s*\(.*\)\s*\{\s*await publishTimetable/);

    expect(publishing).toContain("parsePublishFailure");
    expect(publishing).toContain("publishReadiness");

    expect(soft).not.toContain("publish-readiness");
    expect(soft).not.toContain("isReady");
    expect(soft).toContain("Informational only");

    for (const src of [designer, publishing]) {
      expect(src).not.toContain("createTeachingGroup");
      expect(src).not.toContain("setTimetableSections");
      expect(src).not.toContain("/attendance");
      expect(src).not.toMatch(/publishTimetable\([\s\S]{0,80}publishTimetable\(/);
    }
  });

  it("no client-side capacity or conflict engines in contract layer", () => {
    const util = read("pages", "setup", "scheduling", "publishReadiness.ts");
    expect(util).not.toContain("ExpectedCapacity");
    expect(util).not.toContain("MaxTeachingCapacity >");
    expect(util).not.toContain("marginPercent");
    expect(util).not.toContain("shouldInferTeachingGroup");
  });
});
