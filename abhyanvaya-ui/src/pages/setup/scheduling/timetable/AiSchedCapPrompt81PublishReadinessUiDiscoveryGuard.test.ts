import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const root = resolve(__dirname, "../../../..");
const read = (...parts: string[]) => readFileSync(resolve(root, ...parts), "utf8");
const repoDocs = (...parts: string[]) =>
  readFileSync(resolve(root, "..", "..", "docs", ...parts), "utf8");

/**
 * AI-SCHED-CAP Prompt 8.1 — Publish Readiness UI discovery guards.
 * Discovery only: asserts integration boundary and frozen constraints.
 * Does not implement readiness UI.
 */
describe("AI-SCHED-CAP Prompt 8.1 — Publish Readiness UI discovery", () => {
  it("discovery document exists and is discovery-only", () => {
    const doc = repoDocs("AI_SCHED_CAP_PROMPT_8_1_PUBLISH_READINESS_UI_DISCOVERY.md");
    expect(doc).toContain("DISCOVERY ONLY");
    expect(doc).toContain("no production behavior changed");
    expect(doc).toContain("GET /api/scheduling/timetables/{id}/publish-readiness");
    expect(doc).toContain("SoftWarningsPanel");
    expect(doc).toContain("TimetableDesignerPage");
    expect(doc).toContain("PublishingPage");
    expect(doc).toContain("Recommended insertion points");
    expect(doc).toMatch(/Consume readiness exclusively via API client/i);
    expect(doc).toContain("ConflictEngine");
    expect(doc).toMatch(/Do \*\*not\*\*:/i);
  });

  it("publish API client path remains unchanged; Prompt 8.2 adds readiness client", () => {
    const service = read("services", "schedulingService.ts");
    expect(service).toMatch(
      /publishTimetable\s*=\s*\([^)]*\)\s*=>\s*[\s\S]*?`\/scheduling\/timetables\/\$\{id\}\/publish`/,
    );
    expect(service).toContain("export type PublishTimetableRequest");
    // Prompt 8.2 owns readiness client (discovery gap closed).
    expect(service).toContain("publish-readiness");
    expect(service).toContain("TimetablePublishReadinessResultDto");
    expect(service).toContain("getTimetablePublishReadiness");
  });

  it("readiness must be consumed through API client — no client conflict/capacity engine in designer surfaces", () => {
    const designer = read("pages", "setup", "scheduling", "timetable", "TimetableDesignerPage.tsx");
    const soft = read("pages", "setup", "scheduling", "timetable", "SoftWarningsPanel.tsx");
    const utils = read("pages", "setup", "scheduling", "timetable", "timetableUtils.ts");
    const grid = read("pages", "setup", "scheduling", "timetable", "TimetableGrid.tsx");

    for (const src of [designer, soft, utils, grid]) {
      expect(src).not.toContain("ConflictEngine");
      expect(src).not.toContain("PlacementSizeResolver");
      expect(src).not.toContain("RoomCapacityEvaluator");
      expect(src).not.toContain("EffectiveRoomCapacity =");
      expect(src).not.toContain("ExpectedCapacity *");
      expect(src).not.toContain("1 - margin");
    }

    // Soft warnings present server metrics; they must not invent PlacementSize.
    expect(utils).toContain("entryCapacityFeedbackFromSoftWarnings");
    expect(utils).not.toMatch(/placementSize\s*=\s*.*expectedCapacity/i);
  });

  it("no Teaching Group inference/auto-create in designer publish/readiness surfaces", () => {
    const designer = read("pages", "setup", "scheduling", "timetable", "TimetableDesignerPage.tsx");
    const soft = read("pages", "setup", "scheduling", "timetable", "SoftWarningsPanel.tsx");
    const contract = read(
      "pages",
      "setup",
      "scheduling",
      "timetable",
      "timetableTeachingGroupSelectorContract.ts",
    );

    expect(designer).not.toContain("createTeachingGroup");
    expect(soft).not.toContain("createTeachingGroup");
    expect(soft).not.toContain("assignTeachingGroup");
    expect(contract).toContain("shouldInferTeachingGroupFromSubjectAllocation");
    expect(contract).toMatch(/shouldInferTeachingGroupFromSubjectAllocation[\s\S]*?false/);
  });

  it("SoftWarningsPanel remains informational and separate from publish gate", () => {
    const soft = read("pages", "setup", "scheduling", "timetable", "SoftWarningsPanel.tsx");
    expect(soft).toContain("Informational only");
    expect(soft).toContain("editing is never blocked");
    expect(soft).not.toContain("publish-readiness");
    expect(soft).not.toContain("IsReady");
    expect(soft).not.toContain("isBlocking");

    const designer = read("pages", "setup", "scheduling", "timetable", "TimetableDesignerPage.tsx");
    expect(designer).toContain("publishTimetable");
    expect(designer).toContain("getTimetableSoftWarnings");
    // Prompt 8.2 wires parsePublishFailure; SoftWarnings stay separate.
    expect(designer).toContain("parsePublishFailure");
  });

  it("PublishingPage uses same publishTimetable client; Prompt 8.2 maps publish failures", () => {
    const page = read("pages", "setup", "scheduling", "governance", "PublishingPage.tsx");
    expect(page).toContain("publishTimetable");
    expect(page).toContain("Dialog");
    expect(page).toContain("parsePublishFailure");
  });

  it("designer publish remains Locked + Scheduling.Publish gated", () => {
    const designer = read("pages", "setup", "scheduling", "timetable", "TimetableDesignerPage.tsx");
    expect(designer).toContain("PermissionKeys.SchedulingPublish");
    expect(designer).toContain("TimetableStatus.Locked");
    expect(designer).toContain("handlePublish");
  });
});
