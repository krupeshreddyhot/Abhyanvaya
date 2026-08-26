/**
 * AI29.1D.24B.2 Prompt 6 — live browser validation (Playwright + system Chrome).
 * Does not print auth tokens.
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
const COLLEGE = process.env.COLLEGE_CODE || "1053";
const USER = process.env.ADMIN_USER || "admin";
const PASS = process.env.ADMIN_PASS || "admin123";
const OUT =
  process.env.OUT_DIR ||
  join("D:", "Resheta", "AttendenceProject", "CursonModifiedFiles", "AI Attandance", "AI29.1D.24B.2", "Prompt 6");

const results = [];
const log = (id, status, detail) => {
  results.push({ id, status, detail });
  console.log(`${id}: ${status} — ${detail}`);
};

async function dismissOverlays(page) {
  for (let i = 0; i < 3; i++) {
    await page.keyboard.press("Escape").catch(() => {});
    await page.waitForTimeout(150);
  }
  const backdrop = page.locator(".MuiBackdrop-root");
  if (await backdrop.count()) {
    await page.keyboard.press("Escape").catch(() => {});
    await page.waitForTimeout(200);
  }
}

async function selectMui(page, label, optionPattern) {
  await dismissOverlays(page);
  const field = page.getByLabel(label, { exact: false }).first();
  await field.click({ timeout: 15000 });
  const opt = page.getByRole("option", { name: new RegExp(optionPattern, "i") }).first();
  await opt.waitFor({ state: "visible", timeout: 15000 });
  await opt.click();
  await dismissOverlays(page);
  await page.waitForTimeout(300);
}

async function clickWorkspaceNext(page) {
  const next = page.getByRole("button", { name: /^Next$/i }).first();
  if (!(await next.count()) || (await next.isDisabled())) return false;
  await next.click();
  await page.waitForTimeout(1800);
  return true;
}

async function goToStepLabel(page, label) {
  const step = page.getByText(label, { exact: true }).first();
  if (await step.count()) {
    await step.click({ force: true }).catch(() => {});
    await page.waitForTimeout(800);
  }
}

async function backToAcademicScope(page) {
  await dismissOverlays(page);
  for (let i = 0; i < 8; i++) {
    await dismissOverlays(page);
    const text = await page.locator("body").innerText();
    if (/Academic Year/i.test(text) && /Academic Scope/i.test(text)) return;
    const back = page.getByRole("button", { name: /^Back$/i }).first();
    if (!(await back.count()) || (await back.isDisabled())) break;
    await back.click({ force: true });
    await page.waitForTimeout(500);
  }
  await dismissOverlays(page);
}

async function apiScopeProbe() {
  const loginRes = await fetch(`${API}/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      universityCode: "001",
      collegeCode: COLLEGE,
      username: USER,
      password: PASS,
    }),
  });
  if (!loginRes.ok) return { ok: false, detail: `login HTTP ${loginRes.status}` };
  const loginJson = await loginRes.json();
  const token = loginJson.token || loginJson.accessToken || loginJson.Token;
  if (!token) return { ok: false, detail: "no token field" };

  // Resolve hierarchy ids from academic APIs (best-effort labels).
  const headers = { Authorization: `Bearer ${token}` };
  const yearPaths = [
    "scheduling/academic-years",
    "academic-year",
    "academicyear",
    "v1/academic-structure/academic-years",
  ];
  let yearList = [];
  for (const p of yearPaths) {
    const res = await fetch(`${API}/${p}`, { headers });
    if (!res.ok) continue;
    const json = await res.json().catch(() => null);
    yearList = Array.isArray(json) ? json : json?.items || json?.data || [];
    if (yearList.length) break;
  }
  const year =
    yearList.find((y) => /2026/i.test(String(y.name || y.code || y.label || ""))) || yearList[0];
  if (!year?.id && !year?.academicYearId)
    return { ok: false, detail: `academic year not resolved (tried ${yearPaths.join(",")})` };
  const academicYearId = year.id || year.academicYearId;

  const courseRes = await fetch(`${API}/course`, { headers });
  const courses = courseRes.ok ? await courseRes.json() : [];
  const courseList = Array.isArray(courses) ? courses : courses?.items || [];
  const course = courseList.find((c) => /b\.?\s*com/i.test(String(c.name || c.code || "")));
  if (!course?.id) return { ok: false, detail: "B.Com course not found" };

  const groupsRes = await fetch(`${API}/group?courseId=${course.id}`, { headers });
  const groups = groupsRes.ok ? await groupsRes.json() : [];
  const groupList = Array.isArray(groups) ? groups : groups?.items || [];
  const ca = groupList.find((g) => /computer applications/i.test(String(g.name || g.code || "")));
  const finance = groupList.find((g) => /finance/i.test(String(g.name || g.code || "")));
  if (!ca?.id) return { ok: false, detail: "CA group not found" };

  const semRes = await fetch(`${API}/semester?courseId=${course.id}&groupId=${ca.id}`, { headers });
  const semesters = semRes.ok ? await semRes.json() : [];
  const semList = Array.isArray(semesters) ? semesters : semesters?.items || [];
  const sem =
    semList.find((s) => /\bIII\b|semester\s*3|^3$/i.test(String(s.name || s.code || ""))) ||
    semList[0];
  if (!sem?.id) return { ok: false, detail: "Semester III not found" };

  const ctxUrl = `${API}/allocation/context?academicYearId=${academicYearId}&courseId=${course.id}&groupId=${ca.id}&semesterId=${sem.id}`;
  const ctxRes = await fetch(ctxUrl, { headers });
  if (!ctxRes.ok) return { ok: false, detail: `context HTTP ${ctxRes.status}` };
  const ctx = await ctxRes.json();
  const sections = ctx.sections || [];
  const codes = sections.map((s) => s.sectionCode || s.code || String(s.sectionId));
  const financeLeak = codes.some((c) => /fin/i.test(c));
  return {
    ok: !financeLeak,
    detail: `CA context sections=[${codes.join(", ")}] count=${codes.length} financeLeak=${financeLeak} financeGroupId=${finance?.id ?? "n/a"}`,
    sections,
    academicYearId,
    courseId: course.id,
    groupId: ca.id,
    semesterId: sem.id,
  };
}

async function main() {
  mkdirSync(OUT, { recursive: true });

  // API authority probe for Test A (server Allocation Context)
  try {
    const probe = await apiScopeProbe();
    log("A_API", probe.ok ? "PASS" : "FAIL", probe.detail);
  } catch (e) {
    log("A_API", "FAIL", String(e.message || e));
  }

  const browser = await chromium.launch({ channel: "chrome", headless: true });
  const page = await browser.newPage();
  page.setDefaultTimeout(25000);

  try {
    await page.goto(UI, { waitUntil: "networkidle" });
    await page.waitForTimeout(800);
    try {
      await selectMui(page, "University", "Osmania|001");
    } catch {
      /* auto-selected */
    }
    const college = page.getByLabel(/College/i).first();
    const role = await college.getAttribute("role");
    if (role === "combobox") await selectMui(page, "College", COLLEGE);
    else await college.fill(COLLEGE);
    await page.getByLabel(/Username/i).fill(USER);
    await page.getByLabel(/^Password$/i).fill(PASS);
    await page.getByRole("button", { name: /sign in|login/i }).click();
    await page.waitForURL((u) => !/login/i.test(u.pathname), { timeout: 20000 }).catch(() => {});
    await page.waitForTimeout(1500);
    if (/login/i.test(page.url())) {
      log("LOGIN", "FAIL", page.url());
      throw new Error("Login failed");
    }
    log("LOGIN", "PASS", page.url());

    await page.goto(`${UI}/setup/sections`, { waitUntil: "networkidle" });
    await page.waitForTimeout(1000);
    const tab = page.getByRole("tab", { name: /Allocation Workspace/i });
    if (await tab.count()) await tab.click();
    else throw new Error("Allocation Workspace tab missing");
    await page.waitForTimeout(1000);

    await selectMui(page, "Academic Year", "2026");
    await selectMui(page, "Program", "Commerce");
    await selectMui(page, "Course", "B\\.Com|B.Com|BCom");
    await selectMui(page, "Group", "Computer Applications");
    await selectMui(page, "Semester", "III");
    log("SCOPE_SELECT", "PASS", "2026 / Commerce / B.Com / CA / III");

    // Advance as far as population/rules allow
    for (let i = 0; i < 6; i++) {
      if (!(await clickWorkspaceNext(page))) break;
      const t = await page.locator("body").innerText();
      if (/Target Sections/i.test(t)) break;
    }
    await goToStepLabel(page, "Section Capacity");
    await page.waitForTimeout(1000);
    await page.screenshot({ path: join(OUT, "after-scope-advance.png"), fullPage: true });

    let body = await page.locator("body").innerText();
    if (!/Target Sections/i.test(body)) {
      log(
        "A",
        "FAIL",
        "UI did not reach Target Sections (likely blocked by Student Population gate). See A_API for server scope.",
      );
      log("B", "FAIL", "Blocked before Target Sections");
      log("C", "FAIL", "Blocked before Target Sections");
      log("D", "FAIL", "Blocked before Target Sections");
    } else {
      await dismissOverlays(page);
      const targetRegion = page.locator("text=Target Sections").locator("xpath=ancestor::div[2]");
      const targetText = (await targetRegion.innerText().catch(() => body)) || body;
      const financeLeak = /\bFIN[- ]|\bFinance\b/i.test(targetText) && /checkbox|CA-/i.test(targetText);
      log("A", financeLeak ? "FAIL" : "PASS", financeLeak ? "Finance labels in Target Sections" : "Target Sections visible for CA scope");

      try {
      const allEligible = page.locator('input[type="radio"][value="all"]').first();
      await allEligible.click({ force: true });
      const next = page.getByRole("button", { name: /^Next$/i }).first();
      log("B", (await next.isEnabled()) ? "PASS" : "FAIL", "All eligible mode");

      const explicit = page.locator('input[type="radio"][value="explicit"]').first();
      await explicit.click({ force: true });
      await page.waitForTimeout(500);
      const boxes = page.locator('.MuiFormGroup-root input[type="checkbox"], input[type="checkbox"]');
      const n = await boxes.count();
      if (n < 1) {
        log("C", "FAIL", "No checkboxes");
        log("D", "FAIL", "No checkboxes");
      } else {
        await boxes.nth(0).check({ force: true });
        let t = await page.locator("body").innerText();
        const one = /Selected:\s*1 section\b/i.test(t);
        let two = true;
        let backOk = true;
        if (n > 1) {
          await boxes.nth(1).check({ force: true });
          t = await page.locator("body").innerText();
          two = /Selected:\s*2 sections/i.test(t);
          await boxes.nth(1).uncheck({ force: true });
          t = await page.locator("body").innerText();
          backOk = /Selected:\s*1 section\b/i.test(t);
        }
        log("C", one && two && backOk ? "PASS" : "FAIL", `one=${one} two=${two} deselect=${backOk} boxes=${n}`);

        for (let i = 0; i < n; i++) {
          if (await boxes.nth(i).isChecked()) await boxes.nth(i).uncheck({ force: true });
        }
        await page.waitForTimeout(400);
        t = await page.locator("body").innerText();
        const disabled = await next.isDisabled();
        const msg = /Select at least one Section to continue/i.test(t);
        log("D", disabled && msg ? "PASS" : "FAIL", `disabled=${disabled} message=${msg}`);
      }
      } catch (e) {
        log("B", "FAIL", String(e.message || e));
        log("C", "FAIL", "Skipped after B failure");
        log("D", "FAIL", "Skipped after B failure");
      }
    }

    // E group change
    try {
      await backToAcademicScope(page);
      await selectMui(page, "Group", "Finance");
      for (let i = 0; i < 6; i++) {
        if (!(await clickWorkspaceNext(page))) break;
        if (/Target Sections/i.test(await page.locator("body").innerText())) break;
      }
      await goToStepLabel(page, "Section Capacity");
      body = await page.locator("body").innerText();
      const staleCaSelected = /Selected:\s*[1-9].*CA-A|CA-A.*Selected/i.test(body);
      log(
        "E",
        /Target Sections|No eligible Sections/i.test(body) && !staleCaSelected ? "PASS" : "FAIL",
        staleCaSelected ? "Stale CA selection retained" : "Finance scope reloaded",
      );
    } catch (e) {
      log("E", "FAIL", String(e.message || e));
    }

    // F semester change
    try {
      await backToAcademicScope(page);
      await selectMui(page, "Group", "Computer Applications");
      await selectMui(page, "Semester", "IV");
      for (let i = 0; i < 6; i++) {
        if (!(await clickWorkspaceNext(page))) break;
        if (/Target Sections|No eligible Sections/i.test(await page.locator("body").innerText())) break;
      }
      await goToStepLabel(page, "Section Capacity");
      body = await page.locator("body").innerText();
      log("F", /Target Sections|No eligible Sections/i.test(body) ? "PASS" : "FAIL", "Semester IV advanced");
    } catch (e) {
      log("F", "FAIL", String(e.message || e));
    }

    // G program change
    try {
      await backToAcademicScope(page);
      await page.getByLabel(/Program/i).first().click();
      const opts = page.getByRole("option");
      const oc = await opts.count();
      if (oc > 1) {
        await opts.nth(Math.min(1, oc - 1)).click();
        await page.waitForTimeout(800);
        log("G", "PASS", "Program changed");
      } else log("G", "FAIL", "No alternate Program");
    } catch (e) {
      log("G", "FAIL", String(e.message || e));
    }

    // H zero eligible — try a scope known empty if message appears during F/G
    body = await page.locator("body").innerText();
    if (/No eligible Sections are available for the selected academic scope/i.test(body)) {
      const next = page.getByRole("button", { name: /^Next$/i }).first();
      log("H", (await next.isDisabled()) ? "PASS" : "FAIL", "Zero eligible gate visible");
    } else {
      log("H", "FAIL", "Empty-section academic scope not available in this tenant dataset");
    }

    // I attendance — separate faculty persona not provided
    log(
      "I",
      "FAIL",
      "Faculty no-timetable / timetable / combined attendance not executed (admin session only; no faculty credentials provided)",
    );

    await page.screenshot({ path: join(OUT, "prompt6-final.png"), fullPage: true });
  } catch (e) {
    log("FATAL", "FAIL", String(e.message || e));
    await page.screenshot({ path: join(OUT, "prompt6-fatal.png"), fullPage: true }).catch(() => {});
  } finally {
    writeFileSync(join(OUT, "browser-results.json"), JSON.stringify(results, null, 2));
    await browser.close();
  }
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
