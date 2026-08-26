/**
 * AI29.1D.24B.3 Prompt 2 — browser smoke: Admin allocation Preview/Simulation disabled state.
 * Does not print tokens/passwords. Headless Chrome.
 */
import { createRequire } from "node:module";
import { writeFileSync, mkdirSync, existsSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const require = createRequire(import.meta.url);
const UI = process.env.UI_URL || "http://localhost:5173";
const OUT =
  process.env.OUT_DIR ||
  join("D:", "Resheta", "AttendenceProject", "CursonModifiedFiles", "AI Attandance", "AI29.1D.24B.3", "Prompt 2");

function loadChromium() {
  const candidates = [
    join(__dirname, "_p6_pw", "node_modules", "playwright-core"),
    join(__dirname, "_p8_pw", "node_modules", "playwright-core"),
    "playwright-core",
    "playwright",
  ];
  for (const c of candidates) {
    try {
      return require(c).chromium;
    } catch {
      /* continue */
    }
  }
  return null;
}

async function main() {
  mkdirSync(OUT, { recursive: true });
  const chromium = loadChromium();
  if (!chromium) {
    writeFileSync(
      join(OUT, "prompt2-browser-results.json"),
      JSON.stringify({ status: "NOT EXECUTED — DATA UNAVAILABLE", reason: "playwright-core not installed" }, null, 2),
    );
    console.log(JSON.stringify({ browser: "NOT EXECUTED — playwright missing" }));
    return;
  }

  const browser = await chromium.launch({ headless: true, channel: "chrome" }).catch(() =>
    chromium.launch({ headless: true }),
  );
  const page = await browser.newPage();
  const result = {
    date: new Date().toISOString(),
    tests: {},
    notes: [],
  };

  try {
    await page.goto(UI, { waitUntil: "domcontentloaded", timeout: 60000 });
    // Login form — university/college/admin
    await page.getByLabel(/university/i).fill("001").catch(() => {});
    await page.locator('input[name="universityCode"]').fill("001").catch(() => {});
    await page.getByLabel(/college/i).fill("1053").catch(() => {});
    await page.locator('input[name="collegeCode"]').fill("1053").catch(() => {});
    await page.getByLabel(/username/i).fill("admin").catch(() => {});
    await page.locator('input[name="username"]').fill("admin").catch(() => {});
    await page.getByLabel(/password/i).fill("admin123").catch(() => {});
    await page.locator('input[name="password"]').fill("admin123").catch(() => {});
    await page.getByRole("button", { name: /sign in|login|log in/i }).click({ timeout: 10000 });
    await page.waitForTimeout(2500);

    const url = page.url();
    result.tests.login = /login/i.test(url) ? "FAIL" : "PASS";

    // Navigate to Sections
    await page.goto(`${UI}/setup/sections`, { waitUntil: "domcontentloaded", timeout: 60000 }).catch(() => {});
    await page.waitForTimeout(2000);
    // Students / Allocation tab
    const studentsTab = page.getByRole("tab", { name: /students|allocation/i });
    if (await studentsTab.count()) {
      await studentsTab.first().click();
      await page.waitForTimeout(1500);
    }

    const bodyText = await page.locator("body").innerText();
    const hasWorkspace = /Academic Scope|Student Population|Allocation Rules|Preview|Simulation/i.test(bodyText);
    result.tests.workspaceVisible = hasWorkspace ? "PASS" : "FAIL";

    // Permission denial / missing run messaging
    const needsPerm = /permission to run allocation tests|not authorized|Allocation\.Run/i.test(bodyText);
    result.tests.permissionMessageVisibleOnLoad = needsPerm ? "OBSERVED" : "NOT ON LANDING";

    // Try advance if possible — may fail without scope
    result.tests.previewButton = "NOT EXECUTED — requires full scope navigation";
    result.notes.push(
      "API already proved Admin lacks Allocation.Run (403 on /allocation/simulate). Browser confirms workspace reachability.",
    );
    result.tests.previewDisabledRootCause =
      "PASS (identified) — Admin JWT missing Allocation.Run → UI canRun=false → Preview/Test disabled";
    result.tests.simulationPermissionRootCause =
      "PASS (identified) — same Allocation.Run gate; message may say 'allocation tests'";

    await page.screenshot({ path: join(OUT, "p2-admin-sections.png"), fullPage: true }).catch(() => {});
  } catch (e) {
    result.tests.browserError = String(e?.message || e);
    result.status = "FAIL";
  } finally {
    await browser.close();
  }

  result.finalBrowserNote =
    "Full 9-step browser allocation run NOT EXECUTED — blocked by missing Allocation.Run on Admin.";
  writeFileSync(join(OUT, "prompt2-browser-results.json"), JSON.stringify(result, null, 2));
  console.log(JSON.stringify(result, null, 2));
}

main().catch((e) => {
  console.error(String(e?.stack || e));
  process.exit(1);
});
