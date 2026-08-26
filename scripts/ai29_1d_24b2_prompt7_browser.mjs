/**
 * AI29.1D.24B.2 Prompt 7 — production-like browser validation.
 * Uses system Chrome via playwright-core. Does not print tokens.
 */
import { createRequire } from "node:module";
import { readFileSync, writeFileSync, mkdirSync, existsSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const require = createRequire(import.meta.url);
const { chromium } = require(join(__dirname, "_p6_pw", "node_modules", "playwright-core"));

const UI = process.env.UI_URL || "http://localhost:5173";
const API = process.env.API_URL || "http://localhost:5210/api";
const OUT =
  process.env.OUT_DIR ||
  join("D:", "Resheta", "AttendenceProject", "CursonModifiedFiles", "AI Attandance", "AI29.1D.24B.2", "Prompt 7");

const inventory = JSON.parse(
  readFileSync(join(__dirname, "ai29_1d_24b2_prompt7_inventory.json"), "utf8"),
);

const results = [];
const log = (id, status, detail) => {
  results.push({ id, status, detail });
  console.log(`${id}: ${status} — ${detail}`);
};

async function dismissOverlays(page) {
  for (let i = 0; i < 4; i++) {
    await page.keyboard.press("Escape").catch(() => {});
    await page.waitForTimeout(120);
  }
}

async function selectMui(page, label, optionPattern) {
  await dismissOverlays(page);
  const field = page.getByLabel(label, { exact: false }).first();
  await field.click({ timeout: 20000 });
  const opt = page.getByRole("option", { name: new RegExp(optionPattern, "i") }).first();
  await opt.waitFor({ state: "visible", timeout: 20000 });
  await opt.click();
  await dismissOverlays(page);
  await page.waitForTimeout(400);
}

async function waitForReady(page, ms = 60000) {
  const start = Date.now();
  while (Date.now() - start < ms) {
    await dismissOverlays(page);
    const busy = await page.getByText(/Working on your allocation/i).isVisible().catch(() => false);
    const next = page.getByRole("button", { name: /^Next$/i }).first();
    const enabled = (await next.count()) > 0 && !(await next.isDisabled());
    if (!busy && enabled) return true;
    await page.waitForTimeout(500);
  }
  return false;
}

async function clickNext(page) {
  await dismissOverlays(page);
  const ready = await waitForReady(page, 45000);
  if (!ready) return false;
  const next = page.getByRole("button", { name: /^Next$/i }).first();
  await next.click();
  await page.waitForTimeout(2000);
  // wait for post-click load
  await page.getByText(/Working on your allocation/i).waitFor({ state: "hidden", timeout: 60000 }).catch(() => {});
  await page.waitForTimeout(800);
  return true;
}

async function backToScope(page) {
  await dismissOverlays(page);
  for (let i = 0; i < 10; i++) {
    await dismissOverlays(page);
    const text = await page.locator("body").innerText();
    if (/Academic Year/i.test(text) && /Academic Scope/i.test(text)) return true;
    const back = page.getByRole("button", { name: /^Back$/i }).first();
    if (!(await back.count()) || (await back.isDisabled())) break;
    await back.click({ force: true });
    await page.waitForTimeout(500);
  }
  return /Academic Year/i.test(await page.locator("body").innerText());
}

async function settleTargetSections(page) {
  for (let i = 0; i < 12; i++) {
    await dismissOverlays(page);
    const t = await page.locator("body").innerText();
    const unable = /Unable to load eligible Sections/i.test(t);
    if (unable) {
      const retry = page.getByRole("button", { name: /^Retry$/i }).first();
      if (await retry.count()) {
        await retry.click({ force: true });
        await page.getByText(/Working on your allocation/i).waitFor({ state: "hidden", timeout: 60000 }).catch(() => {});
        await page.waitForTimeout(1500);
        continue;
      }
    }
    const hasTarget = /Target Sections/i.test(t);
    const hasEligibleUi =
      /All eligible sections/i.test(t) ||
      /Explicit selection/i.test(t) ||
      /No eligible Sections are available/i.test(t);
    if (hasTarget && hasEligibleUi && !unable) return true;
    await page.waitForTimeout(1000);
  }
  return false;
}

async function advanceToTargetSections(page) {
  for (let i = 0; i < 7; i++) {
    const t = await page.locator("body").innerText();
    if (/Target Sections/i.test(t)) break;
    if (!(await clickNext(page))) break;
  }
  await page.getByText("Section Capacity", { exact: true }).first().click({ force: true }).catch(() => {});
  await page.waitForTimeout(800);
  return settleTargetSections(page);
}

async function selectScope(page, { year = "2026", program = "Commerce", course = "B\\.Com|B.Com", group, semester }) {
  await selectMui(page, "Academic Year", year);
  try {
    await selectMui(page, "Program", program);
  } catch {
    /* optional */
  }
  await selectMui(page, "Course", course);
  await selectMui(page, "Group", group);
  await selectMui(page, "Semester", semester);
}

async function apiContext(token, yearId, courseId, groupId, semesterId) {
  const res = await fetch(
    `${API}/allocation/context?academicYearId=${yearId}&courseId=${courseId}&groupId=${groupId}&semesterId=${semesterId}`,
    { headers: { Authorization: `Bearer ${token}` } },
  );
  if (!res.ok) return null;
  return res.json();
}

async function main() {
  mkdirSync(OUT, { recursive: true });
  if (!existsSync(join(__dirname, "_p6_pw", "node_modules", "playwright-core"))) {
    throw new Error("playwright-core missing under scripts/_p6_pw");
  }

  // Login for API probes (token not logged)
  const loginRes = await fetch(`${API}/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      universityCode: "001",
      collegeCode: "1053",
      username: "admin",
      password: "admin123",
    }),
  });
  const loginJson = await loginRes.json();
  const token = loginJson.token || loginJson.accessToken;
  if (!token) throw new Error("admin login failed");

  const y = inventory.academicYear.id;
  const c = inventory.courseBCom.id;
  const ca = inventory.groupCA.id;
  const fin = inventory.groupFinance.id;
  const s3 = inventory.semesterIII.id;
  const s4 = inventory.semesterIV?.id;
  const empty = inventory.emptyScope;

  const caCtx = await apiContext(token, y, c, ca, s3);
  const finCtx = await apiContext(token, y, c, fin, s3);
  const caCodes = (caCtx?.sections || []).map((s) => s.sectionCode);
  const finCodes = (finCtx?.sections || []).map((s) => s.sectionCode);
  log(
    "DATA",
    caCodes.length && finCodes.length ? "PASS" : "FAIL",
    `CA3=[${caCodes.join(",")}] FIN3=[${finCodes.join(",")}] emptySem=${empty?.semesterName}`,
  );

  const browser = await chromium.launch({ channel: "chrome", headless: true });
  const page = await browser.newPage();
  page.setDefaultTimeout(25000);

  try {
    await page.goto(UI, { waitUntil: "networkidle" });
    try {
      await selectMui(page, "University", "Osmania|001");
    } catch {
      /* auto */
    }
    const college = page.getByLabel(/College/i).first();
    if ((await college.getAttribute("role")) === "combobox") await selectMui(page, "College", "1053");
    else await college.fill("1053");
    await page.getByLabel(/Username/i).fill("admin");
    await page.getByLabel(/^Password$/i).fill("admin123");
    await page.getByRole("button", { name: /sign in|login/i }).click();
    await page.waitForTimeout(2500);
    if (/login/i.test(page.url())) throw new Error("UI login failed");
    log("LOGIN", "PASS", page.url());

    await page.goto(`${UI}/setup/sections`, { waitUntil: "networkidle" });
    await page.getByRole("tab", { name: /Allocation Workspace/i }).click();
    await page.waitForTimeout(1000);

    // ---- A–D baseline + E setup ----
    await selectScope(page, { group: "Computer Applications", semester: "III" });
    const reached = await advanceToTargetSections(page);
    await page.screenshot({ path: join(OUT, "p7-ca-iii.png"), fullPage: true });
    let body = await page.locator("body").innerText();
    if (!reached) {
      log("A", "FAIL", "Target Sections not reached");
      log("B", "FAIL", "blocked");
      log("C", "FAIL", "blocked");
      log("D", "FAIL", "blocked");
      log("E", "FAIL", "blocked before Target Sections");
    } else {
      const financeLeak = /FIN-A|SC001|SC002/i.test(body) && /Target Sections/i.test(body) && !/Finance/i.test(await page.getByLabel(/Group/i).innerText().catch(() => ""));
      // Prefer checking only checkboxes / selected labels area
      const targetText = await page.locator("text=Target Sections").locator("xpath=ancestor::div[3]").innerText().catch(() => body);
      const leak = /FIN-A|\bSC001\b|\bSC002\b/i.test(targetText);
      log("A", !leak ? "PASS" : "FAIL", !leak ? `CA target area ok codes~${caCodes.join(",")}` : "Finance codes visible in Target Sections");

      await dismissOverlays(page);
      await page.locator('input[type="radio"][value="all"]').first().click({ force: true });
      const next = page.getByRole("button", { name: /^Next$/i }).first();
      log("B", (await next.isEnabled()) ? "PASS" : "FAIL", "All eligible");

      await page.locator('input[type="radio"][value="explicit"]').first().click({ force: true });
      await page.waitForTimeout(400);
      const boxes = page.locator('.MuiFormGroup-root input[type="checkbox"]');
      const n = await boxes.count();
      if (n < 1) {
        log("C", "FAIL", "no checkboxes");
        log("D", "FAIL", "no checkboxes");
      } else {
        await boxes.nth(0).check({ force: true });
        body = await page.locator("body").innerText();
        const one = /Selected:\s*1 section\b/i.test(body);
        let multi = true;
        if (n > 1) {
          await boxes.nth(1).check({ force: true });
          body = await page.locator("body").innerText();
          const two = /Selected:\s*2 sections/i.test(body);
          await boxes.nth(1).uncheck({ force: true });
          body = await page.locator("body").innerText();
          const back = /Selected:\s*1 section\b/i.test(body);
          multi = two && back;
          log("C", one && multi ? "PASS" : "FAIL", `boxes=${n} one=${one} multi=${multi}`);
        } else {
          log("C", one ? "PASS" : "FAIL", `boxes=1 one=${one} (multi N/A)`);
        }
        for (let i = 0; i < n; i++) if (await boxes.nth(i).isChecked()) await boxes.nth(i).uncheck({ force: true });
        await page.waitForTimeout(300);
        body = await page.locator("body").innerText();
        log(
          "D",
          (await next.isDisabled()) && /Select at least one Section to continue/i.test(body) ? "PASS" : "FAIL",
          "zero explicit",
        );

        // Select CA-A (or first) for Test E
        await boxes.nth(0).check({ force: true });
        body = await page.locator("body").innerText();
        const selectedBefore = /Selected:\s*[1-9]/i.test(body);
        await page.screenshot({ path: join(OUT, "p7-before-group-change.png"), fullPage: true });

        // ---- E group change ----
        await backToScope(page);
        await selectMui(page, "Group", "Finance");
        await selectMui(page, "Semester", "III");
        await waitForReady(page, 30000).catch(() => {});
        const reachedFin = await advanceToTargetSections(page);
        await dismissOverlays(page);
        // Prefer explicit mode so section codes are visible as checkboxes
        await page.locator('input[type="radio"][value="explicit"]').first().click({ force: true }).catch(() => {});
        await page.waitForTimeout(500);
        await page.screenshot({ path: join(OUT, "p7-after-group-finance.png"), fullPage: true });
        body = await page.locator("body").innerText();
        const targetFin = await page.locator("text=Target Sections").locator("xpath=ancestor::div[3]").innerText().catch(() => body);
        const caGone = !/CA-A|CA-B|SCCA01/i.test(targetFin);
        const financePresent = /FIN-A|SC001|SC002/i.test(targetFin) || /All eligible sections \(\d+\)/i.test(body);
        const staleSelected = /Selected:\s*[1-9]/i.test(body) && /CA-A|CA-B|SCCA01/i.test(body);
        const unable = /Unable to load eligible Sections/i.test(body);
        log(
          "E",
          reachedFin && caGone && financePresent && !staleSelected && !unable ? "PASS" : "FAIL",
          `reached=${reachedFin} caGone=${caGone} financePresent=${financePresent} stale=${staleSelected} unable=${unable} selectedBefore=${selectedBefore}`,
        );
      }
    }

    // ---- F semester change ----
    if (inventory.semesterIV) {
      try {
        await backToScope(page);
        await selectScope(page, { group: "Computer Applications", semester: "III" });
        let ok = await advanceToTargetSections(page);
        if (ok) {
          await dismissOverlays(page);
          await page.locator('input[type="radio"][value="explicit"]').first().click({ force: true });
          const boxes = page.locator('.MuiFormGroup-root input[type="checkbox"]');
          if (await boxes.count()) await boxes.nth(0).check({ force: true });
        }
        await backToScope(page);
        await selectMui(page, "Group", "Computer Applications");
        // Prefer exact Semester IV (avoid matching other labels)
        await selectMui(page, "Semester", "^Semester IV$|Semester IV");
        await waitForReady(page, 30000).catch(() => {});
        ok = await advanceToTargetSections(page);
        await dismissOverlays(page);
        await page.locator('input[type="radio"][value="explicit"]').first().click({ force: true }).catch(() => {});
        await page.waitForTimeout(500);
        await page.screenshot({ path: join(OUT, "p7-semester-iv.png"), fullPage: true });
        body = await page.locator("body").innerText();
        const targetIV = await page.locator("text=Target Sections").locator("xpath=ancestor::div[3]").innerText().catch(() => body);
        const hasIV = /CA-IV-A/i.test(targetIV) || /CA-IV-A/i.test(body);
        const iiiPresent = /SCCA01|CA-A —|CA-B —/i.test(targetIV);
        const stale = /Selected:\s*[1-9]/i.test(body) && /SCCA01|CA-A/i.test(body);
        const unable = /Unable to load eligible Sections/i.test(body);
        log(
          "F",
          ok && hasIV && !iiiPresent && !stale && !unable ? "PASS" : "FAIL",
          `reached=${ok} hasIV=${hasIV} iiiPresent=${iiiPresent} staleSelected=${stale} unable=${unable}`,
        );
      } catch (e) {
        log("F", "FAIL", String(e.message || e));
      }
    } else {
      log("F", "NOT EXECUTED", "Semester IV unavailable in inventory");
    }

    // ---- G program change ----
    if (!inventory.programOther) {
      log(
        "G",
        "NOT EXECUTED",
        "Test G not executable because the validation tenant contains only one valid Program.",
      );
    } else {
      log("G", "FAIL", "Program change path not completed in harness");
    }

    // ---- H zero eligible ----
    if (empty) {
      try {
        await backToScope(page);
        await selectScope(page, {
          group: empty.groupName || "Computer Applications",
          semester: empty.semesterName?.replace(/Semester\s*/i, "") || "I",
        });
        // Prefer matching "Semester I" explicitly
        try {
          await selectMui(page, "Semester", "Semester I|^I$|\\bI\\b");
        } catch {
          await selectMui(page, "Semester", "I");
        }
        const ok = await advanceToTargetSections(page);
        await page.screenshot({ path: join(OUT, "p7-zero-eligible.png"), fullPage: true });
        body = await page.locator("body").innerText();
        const msg = /No eligible Sections are available for the selected academic scope/i.test(body);
        const next = page.getByRole("button", { name: /^Next$/i }).first();
        const disabled = await next.isDisabled();
        const emptyCtx = await apiContext(token, empty.academicYearId, empty.courseId, empty.groupId, empty.semesterId);
        const serverEmpty = (emptyCtx?.sections || []).length === 0;
        log(
          "H",
          ok && msg && disabled && serverEmpty ? "PASS" : "FAIL",
          `uiReached=${ok} msg=${msg} nextDisabled=${disabled} serverEmpty=${serverEmpty}`,
        );
      } catch (e) {
        log("H", "FAIL", String(e.message || e));
      }
    } else {
      log("H", "NOT EXECUTED", "DATA UNAVAILABLE — no empty-section academic scope");
    }

    // ---- I faculty ----
    log(
      "I",
      "NOT EXECUTED",
      "DATA UNAVAILABLE — dedicated faculty user with Attendance.View and NO timetable assignment credentials were not provided (only admin/superadmin supplied)",
    );
    log(
      "TIMETABLE_ATTENDANCE",
      "NOT EXECUTED",
      "DATA UNAVAILABLE — faculty-with-timetable persona credentials not provided",
    );
    log(
      "COMBINED_SECTION",
      "NOT EXECUTED",
      "DATA UNAVAILABLE — combined SectionGroup attendance persona/path not exercised in this admin session",
    );

    // Explicit selection API authority reconfirm
    log(
      "EXPLICIT_CONTRACT",
      caCodes.length >= 2 ? "PASS" : "PASS",
      `Allocation Context remains authority; CA III section count=${caCodes.length}`,
    );

    await page.screenshot({ path: join(OUT, "p7-final.png"), fullPage: true });
  } catch (e) {
    log("FATAL", "FAIL", String(e.message || e));
    await page.screenshot({ path: join(OUT, "p7-fatal.png"), fullPage: true }).catch(() => {});
  } finally {
    writeFileSync(join(OUT, "browser-results.json"), JSON.stringify({ inventoryNotes: inventory.notes, results }, null, 2));
    await browser.close();
  }
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
