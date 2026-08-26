import { createRequire } from "node:module";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";

const __dirname = dirname(fileURLToPath(import.meta.url));
const require = createRequire(import.meta.url);
const { chromium } = require(join(__dirname, "_p6_pw", "node_modules", "playwright-core"));
const UI = process.env.UI_URL || "http://localhost:5173";
const OUT = join(
  "D:",
  "Resheta",
  "AttendenceProject",
  "CursonModifiedFiles",
  "AI Attandance",
  "AI29.1D.24B.2",
  "Prompt 8",
);

spawnSync(process.execPath, [join(__dirname, "ai29_1d_24b2_prompt8_persona_prep.mjs")], {
  cwd: join(__dirname, ".."),
  stdio: "inherit",
  env: process.env,
});

async function selectMui(page, label, optionPattern) {
  await page.keyboard.press("Escape").catch(() => {});
  await page.getByLabel(label, { exact: false }).first().click({ timeout: 20000 });
  await page.getByRole("option", { name: new RegExp(optionPattern, "i") }).first().click();
  await page.waitForTimeout(700);
}

const browser = await chromium.launch({ channel: "chrome", headless: true });
const page = await browser.newPage();
await page.goto(UI, { waitUntil: "networkidle" });
try {
  await selectMui(page, "University", "Osmania|001");
} catch {
  /* auto */
}
const college = page.getByLabel(/College/i).first();
if ((await college.getAttribute("role")) === "combobox") await selectMui(page, "College", "1053");
else await college.fill("1053");
await page.getByLabel(/Username/i).fill("teststaff1");
await page.getByLabel(/^Password$/i).fill("teststaff1f");
await page.getByRole("button", { name: /sign in|login/i }).click();
await page.waitForTimeout(2500);
if (/change-password/i.test(page.url())) {
  const np = "teststaff1fA1";
  await page.getByLabel(/Current password/i).fill("teststaff1f");
  await page.getByLabel(/^New password/i).fill(np);
  await page.getByLabel(/Confirm new password/i).fill(np);
  await page.getByRole("button", { name: /save|change|update|continue/i }).first().click();
  await page.waitForTimeout(2500);
  if (/login/i.test(page.url())) {
    await page.getByLabel(/Username/i).fill("teststaff1");
    await page.getByLabel(/^Password$/i).fill(np);
    await page.getByRole("button", { name: /sign in|login/i }).click();
    await page.waitForTimeout(2500);
  }
}
await page.goto(`${UI}/attendance`, { waitUntil: "networkidle" });
await page.waitForTimeout(1500);
await selectMui(page, "Course", "B\\.Com|B.Com");
await selectMui(page, "Group", "Computer|COMPUTER");
await selectMui(page, "Semester", "III");
await page.waitForTimeout(2500);
const helper = await page.getByLabel(/Section/i).locator("xpath=ancestor::div[contains(@class,'MuiFormControl')]").innerText().catch(() => "");
console.log("sectionHelper:", helper.replace(/\s+/g, " ").slice(0, 300));
const disabled = await page.getByLabel(/Section/i).first().isDisabled().catch(() => null);
console.log("sectionDisabled:", disabled);
await page.getByLabel(/Section/i).first().click({ force: true }).catch((e) => console.log("click err", e.message));
await page.waitForTimeout(1000);
const opts = await page.getByRole("option").allInnerTexts().catch(() => []);
console.log("optionCount:", opts.length);
console.log("options:", opts.slice(0, 20));
await page.screenshot({ path: join(OUT, "p8-section-debug.png"), fullPage: true });
await browser.close();
