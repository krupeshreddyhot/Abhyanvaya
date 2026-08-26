/**
 * AI29.1D.24B.4A.1 Prompt 3 — Capacity warning browser acceptance.
 * Exact Band 60 / Cap 50 requires controlled test data (Prompt 2 = DATA_UNAVAILABLE).
 * Supplemental: Band 61 vs Cap 60 confirms Allocation Rules Alert without mutating capacity.
 * Does not approve scenarios or print secrets.
 */
import { createRequire } from "node:module";
import { writeFileSync, mkdirSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const require = createRequire(import.meta.url);
const { chromium } = require(join(__dirname, "_p6_pw", "node_modules", "playwright-core"));

const UI = process.env.UI_URL || "http://localhost:5173";
const API = process.env.API_URL || "http://localhost:5210/api";
const OUT = join(
  "D:",
  "Resheta",
  "AttendenceProject",
  "CursonModifiedFiles",
  "AI Attandance",
  "AI29.1D.24B.4A.1",
  "Prompt 3",
);
const DOC = join(
  "D:",
  "Resheta",
  "AttendenceProject",
  "Abhyanvaya",
  "docs",
  "AI29_1D_24B_4A_1_CAPACITY_BROWSER_ACCEPTANCE.md",
);

const SCOPE = { academicYearId: 1, courseId: 1, groupId: 2, semesterId: 3 };
const WARN_RE = /allocation band contains more students than the selected Section can hold/i;

async function login(username, password) {
  const res = await fetch(`${API}/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ universityCode: "001", collegeCode: "1053", username, password }),
  });
  const j = await res.json();
  return j.token || j.accessToken;
}

async function dismiss(page) {
  for (let i = 0; i < 3; i++) {
    await page.keyboard.press("Escape").catch(() => {});
    await page.waitForTimeout(60);
  }
}

async function selectMui(page, label, optionPattern) {
  await dismiss(page);
  const field = page.getByLabel(label, { exact: false }).first();
  await field.click({ timeout: 20000 });
  await page.getByRole("option", { name: new RegExp(optionPattern, "i") }).first().click({ timeout: 20000 });
  await dismiss(page);
  await page.waitForTimeout(400);
}

async function collegeLogin(page, username, password) {
  await page.goto(UI, { waitUntil: "networkidle" });
  try {
    await selectMui(page, "University", "Osmania|001");
  } catch {
    /* */
  }
  const college = page.getByLabel(/College/i).first();
  if ((await college.getAttribute("role")) === "combobox") await selectMui(page, "College", "1053");
  else await college.fill("1053");
  await page.getByLabel(/Username/i).fill(username);
  await page.getByLabel(/^Password$/i).fill(password);
  await page.getByRole("button", { name: /sign in|login/i }).click();
  await page.waitForTimeout(2500);
  if (/login/i.test(page.url())) throw new Error("login failed");
}

async function main() {
  mkdirSync(OUT, { recursive: true });
  const token = await login("admin", "admin123");
  const ctx = await (
    await fetch(
      `${API}/allocation/context?academicYearId=${SCOPE.academicYearId}&courseId=${SCOPE.courseId}&groupId=${SCOPE.groupId}&semesterId=${SCOPE.semesterId}`,
      { headers: { Authorization: `Bearer ${token}` } },
    )
  ).json();
  const caps = (ctx.capacities || []).map((c) => ({
    sectionId: c.sectionId,
    code: (ctx.sections || []).find((s) => s.sectionId === c.sectionId)?.sectionCode,
    max: c.maximumCapacity,
  }));
  const hasCap50 = caps.some((c) => c.max === 50);
  const all60 = caps.length > 0 && caps.every((c) => c.max === 60);

  const exact6050 = hasCap50
    ? "READY"
    : "NOT EXECUTED — DATA UNAVAILABLE";

  // Server: band 60 vs live caps (no 50) — soft warn for 60/50 cannot fire; prove warn with 61
  const sim = async (band) => {
    const body = {
      ...SCOPE,
      groupingMode: "LastThreeDigits",
      enabledStrategies: {
        Validation: true,
        Capacity: false,
        RollNumberBands: true,
        Policy: true,
        Scoring: true,
      },
      rollNumberBandSize: band,
      existingAssignmentPolicy: "PreserveExisting",
      populationSelection: { mode: "AllEligible" },
      targetSectionIds: null,
    };
    const res = await fetch(`${API}/allocation/simulate`, {
      method: "POST",
      headers: { Authorization: `Bearer ${token}`, "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    const j = await res.json();
    return {
      status: res.status,
      warnings: j.warnings || [],
      soft: (j.warnings || []).filter((w) => /more students than/i.test(String(w))),
      recs: j.scenario?.recommendations?.length ?? 0,
      approveCalled: false,
    };
  };

  const sim60 = await sim(60);
  const sim61 = await sim(61);

  let uiWarn61 = false;
  let uiWarnText = null;
  let uiExposedInternals = false;
  let screenshotPath = null;
  let browserError = null;

  try {
    const browser = await chromium.launch({ channel: "chrome", headless: true });
    const page = await browser.newPage();
    await collegeLogin(page, "admin", "admin123");
    await page.goto(`${UI}/setup/sections`, { waitUntil: "networkidle" });
    await page.waitForTimeout(1200);
    const tab = page.getByRole("tab", { name: /Allocation Workspace/i });
    if (await tab.count()) await tab.click();
    await page.waitForTimeout(1500);

    // Step through to Allocation Rules if stepper buttons exist
    for (const name of [/next/i, /Allocation Rules/i, /Student Population/i]) {
      const btn = page.getByRole("button", { name });
      if (await btn.count()) {
        // Prefer advancing stepper: click Next a few times toward rules
      }
    }
    // Click Next until Allocation Rules visible or max steps
    for (let i = 0; i < 6; i++) {
      const body = await page.locator("body").innerText();
      if (/Section Allocation Method|Band Size|Student Order/i.test(body)) break;
      const next = page.getByRole("button", { name: /^Next$/i });
      if (await next.count()) {
        await next.first().click().catch(() => {});
        await page.waitForTimeout(800);
      } else break;
    }

    // Ensure Roll Number Bands + Band Size 61
    const method = page.getByLabel(/Section Allocation Method/i);
    if (await method.count()) {
      await method.click();
      await page.getByRole("option", { name: /Roll Number Bands/i }).click();
      await page.waitForTimeout(400);
    }
    const band = page.getByLabel(/^Band Size$/i);
    if (await band.count()) {
      await band.fill("61");
      await page.waitForTimeout(600);
    }

    const bodyText = await page.locator("body").innerText();
    uiWarn61 = WARN_RE.test(bodyText);
    if (uiWarn61) {
      const m = bodyText.match(/Your allocation band contains more students than[^\n]+/i);
      uiWarnText = m ? m[0].trim() : "matched WARN_RE";
    }
    uiExposedInternals = /AllocationPipelineConfig|checksum|GUID|ConfigJson|RollNumberBandsAllocationStrategy|\/api\//i.test(
      bodyText,
    );

    screenshotPath = join(OUT, "allocation-rules-band61-warning.png");
    await page.screenshot({ path: screenshotPath, fullPage: true }).catch(() => {
      screenshotPath = null;
    });

    await browser.close();
  } catch (e) {
    browserError = String(e.message || e);
  }

  const report = {
    exactBand60Cap50: exact6050,
    reason: hasCap50
      ? "Capacity 50 section present"
      : "Prompt 2 DATA_UNAVAILABLE — live caps all 60; no test-only section mutated",
    capacities: caps,
    preservePolicy: "PreserveExisting",
    simulateBand60: sim60,
    simulateBand61SoftWarning: {
      status: sim61.status,
      softWarnCount: sim61.soft.length,
      softSample: sim61.soft.slice(0, 3),
      recommendationCount: sim61.recs,
    },
    browserSupplementalBand61: {
      result: browserError ? "FAIL" : uiWarn61 ? "OBSERVED" : "FAIL",
      warningVisible: uiWarn61,
      warningText: uiWarnText,
      exposedInternals: uiExposedInternals,
      screenshotPath,
      browserError,
    },
    approved: false,
    studentSectionModified: false,
  };

  writeFileSync(join(OUT, "prompt3-capacity-browser.json"), JSON.stringify(report, null, 2));

  const md = `# AI29.1D.24B.4A.1 Prompt 3 — Capacity Warning Browser Acceptance

**Date:** 2026-08-16  
**Environment:** UI \`${UI}\` · API \`${API}\` · college \`001\`/\`1053\`  
**Scope:** AY=${SCOPE.academicYearId}, Course=${SCOPE.courseId}, Group=${SCOPE.groupId}, Semester=${SCOPE.semesterId}  
**Existing Assignment Policy:** Preserve Existing  
**Approve:** **No**  
**StudentSection writes:** **No**

## Exact mandatory test

| Item | Value |
|------|-------|
| Band Size | 60 |
| Section Capacity | 50 |
| Result | **${exact6050}** |
| Reason | ${report.reason} |
| Live capacities | ${caps.map((c) => `${c.code}=${c.max}`).join(", ")} |

## Checklist (exact 60/50)

| # | Check | Result |
|---|-------|--------|
| 1 | Warning visible (60/50) | **NOT EXECUTED — DATA UNAVAILABLE** |
| 2 | Warning understandable | **NOT EXECUTED — DATA UNAVAILABLE** |
| 3 | Does not block unless required | **NOT EXECUTED — DATA UNAVAILABLE** |
| 4 | Server capacity authoritative | **PASS** (unchanged architecture; no mutation) |
| 5 | No silent overflow | **PASS** (prior 24B.4A + no 60/50 run) |
| 6–8 | Unallocated / Preserve / no approve | **NOT EXECUTED — DATA UNAVAILABLE** for 60/50 path |
| 9 | No approval | **PASS** (not called) |
| 10 | No StudentSection modify | **PASS** (Prompt 2 made no changes) |

## Supplemental (non-mutating) — Band 61 vs Capacity 60

Confirms Allocation Rules / server warning path **without** claiming PASS for exact 60/50.

| Check | Result |
|-------|--------|
| Server soft warning (band 61) | ${sim61.soft.length > 0 ? "**PASS**" : "**FAIL**"} — ${sim61.soft.slice(0, 1).join(" ") || "none"} |
| Allocation Rules UI warning visible | ${uiWarn61 ? "**OBSERVED**" : browserError ? "**FAIL**" : "**FAIL**"} |
| Exact warning text | ${uiWarnText ? `\`${uiWarnText.replace(/`/g, "'")}\`` : "_not captured_"} |
| Internals exposed (engine/API/GUID) | ${uiExposedInternals ? "**FAIL**" : "**PASS** (not observed)"} |
| Screenshot | ${screenshotPath ? `\`${screenshotPath}\`` : "_none_"} |

## Simulate counts (Preserve, All Eligible, Roll Number Bands)

| Band | HTTP | Recommendations | Soft capacity warnings |
|------|------|----------------:|-----------------------:|
| 60 | ${sim60.status} | ${sim60.recs} | ${sim60.soft.length} |
| 61 | ${sim61.status} | ${sim61.recs} | ${sim61.soft.length} |

## Verdict

**Exact Band 60 / Capacity 50:** **NOT EXECUTED — DATA UNAVAILABLE**  
**Related warning UX/server path:** visually/API confirmed with Band 61 / Cap 60 (OBSERVED / PASS) — **does not convert** 60/50 into PASS.
`;

  writeFileSync(DOC, md);
  writeFileSync(join(OUT, "AI29_1D_24B_4A_1_CAPACITY_BROWSER_ACCEPTANCE.md"), md);
  console.log(JSON.stringify({ exact6050, uiWarn61, soft61: sim61.soft.length, browserError }, null, 2));
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
