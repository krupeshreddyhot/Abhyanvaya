import { writeFileSync, mkdirSync } from "node:fs";
import { join } from "node:path";

const API = process.env.API_URL || "http://localhost:5210/api";
const OUT =
  process.env.OUT_DIR ||
  join("D:", "Resheta", "AttendenceProject", "CursonModifiedFiles", "AI Attandance", "AI29.1D.24B.3", "Prompt 2");

function extractPermissions(token) {
  const raw = Buffer.from(token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/"), "base64").toString("utf8");
  const perms = [...raw.matchAll(/"permission"\s*:\s*"([^"]+)"/g)].map((m) => m[1]);
  return [...new Set(perms)].sort();
}

async function readJson(res) {
  const text = await res.text();
  try {
    return { ok: res.ok, status: res.status, body: text ? JSON.parse(text) : null, text: text.slice(0, 300) };
  } catch {
    return { ok: res.ok, status: res.status, body: null, text: text.slice(0, 300) };
  }
}

async function main() {
  const login = await readJson(
    await fetch(`${API}/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        universityCode: "001",
        collegeCode: "1053",
        username: "admin",
        password: "admin123",
      }),
    }),
  );
  const token = login.body?.token || login.body?.accessToken;
  if (!token) {
    console.log(JSON.stringify({ error: "admin login failed", status: login.status, text: login.text }));
    return;
  }

  const permissions = extractPermissions(token);
  const allocation = permissions.filter((p) => p.startsWith("Allocation"));
  const claims = {
    permissionCount: permissions.length,
    hasAllocationRun: permissions.includes("Allocation.Run"),
    hasAllocationApprove: permissions.includes("Allocation.Approve"),
    hasAllocationOpsView: permissions.includes("Allocation.Operations.View"),
    hasSectionView: permissions.includes("Section.View"),
    allocationKeys: allocation,
  };

  const sim = await readJson(
    await fetch(`${API}/allocation/simulate`, {
      method: "POST",
      headers: { "Content-Type": "application/json", Authorization: `Bearer ${token}` },
      body: JSON.stringify({
        academicYearId: 1,
        courseId: 1,
        groupId: 2,
        semesterId: 3,
        groupingMode: "LastThreeDigits",
        enabledStrategies: { Capacity: true, Validation: true },
        populationSelection: { mode: "AllEligible" },
      }),
    }),
  );

  const run = await readJson(
    await fetch(`${API}/allocation/run`, {
      method: "POST",
      headers: { "Content-Type": "application/json", Authorization: `Bearer ${token}` },
      body: JSON.stringify({
        academicYearId: 1,
        courseId: 1,
        groupId: 2,
        semesterId: 3,
        groupingMode: "Alphabetical",
        enabledStrategies: { Capacity: true },
        populationSelection: { mode: "AllEligible" },
      }),
    }),
  );

  // Who am I / roles endpoints if any
  const meCandidates = ["/auth/me", "/users/me", "/tenant-users/me"];
  const me = {};
  for (const p of meCandidates) {
    const r = await readJson(await fetch(`${API}${p}`, { headers: { Authorization: `Bearer ${token}` } }));
    me[p] = { status: r.status, keys: r.body ? Object.keys(r.body).slice(0, 12) : null };
  }

  // RBAC: list current user roles via admin APIs
  const users = await readJson(await fetch(`${API}/tenant-users?pageSize=50`, { headers: { Authorization: `Bearer ${token}` } }));
  const userList = Array.isArray(users.body) ? users.body : users.body?.items || users.body?.data || [];
  const adminUser = userList.find((u) => String(u.username || u.userName || "").toLowerCase() === "admin");

  mkdirSync(OUT, { recursive: true });
  const out = {
    claims,
    simulate: { status: sim.status, text: sim.text },
    run: { status: run.status, text: run.text },
    me,
    adminUserFound: !!adminUser,
    adminUserId: adminUser?.id || adminUser?.userId || null,
    note: "No tokens printed",
  };
  writeFileSync(join(OUT, "prompt2-admin-permission-probe.json"), JSON.stringify(out, null, 2));
  console.log(JSON.stringify(out, null, 2));
}

main().catch((e) => {
  console.error(String(e?.stack || e));
  process.exit(1);
});
