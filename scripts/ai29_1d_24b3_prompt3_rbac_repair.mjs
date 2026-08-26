/**
 * AI29.1D.24B.3 Prompt 3 — Repair Admin ApplicationRole Allocation.* permissions via tenant-rbac API.
 * Merges missing Allocation permission ids into ADMIN role without removing existing grants.
 * Does not print tokens/passwords.
 */
import { writeFileSync, mkdirSync } from "node:fs";
import { join } from "node:path";

const API = process.env.API_URL || "http://localhost:5210/api";
const OUT =
  process.env.OUT_DIR ||
  join("D:", "Resheta", "AttendenceProject", "CursonModifiedFiles", "AI Attandance", "AI29.1D.24B.3", "Prompt 3");

const ALLOCATION_KEYS = [
  "Allocation.Run",
  "Allocation.Approve",
  "Allocation.Operations.View",
  "Allocation.Scenario.View",
  "Allocation.Scenario.Create",
  "Allocation.Scenario.Compare",
  "Allocation.Scenario.Replay",
  "Allocation.Scenario.Review",
  "Allocation.Reject",
  "Allocation.Export",
  "Allocation.Scenario.Archive",
];

async function api(token, path, opts = {}) {
  const res = await fetch(`${API}${path}`, {
    ...opts,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(opts.headers || {}),
    },
  });
  const text = await res.text();
  let body = null;
  try {
    body = text ? JSON.parse(text) : null;
  } catch {
    body = text?.slice?.(0, 400) ?? text;
  }
  return { ok: res.ok, status: res.status, body };
}

function extractPermissions(token) {
  const raw = Buffer.from(token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/"), "base64").toString("utf8");
  const payload = JSON.parse(raw);
  const perms = Array.isArray(payload.permission)
    ? payload.permission
    : payload.permission
      ? [payload.permission]
      : [];
  return perms.map(String);
}

async function main() {
  mkdirSync(OUT, { recursive: true });
  const report = { date: new Date().toISOString(), steps: [] };

  const login = await api(null, "/auth/login", {
    method: "POST",
    body: JSON.stringify({
      universityCode: "001",
      collegeCode: "1053",
      username: "admin",
      password: "admin123",
    }),
  });
  const token = login.body?.token || login.body?.accessToken;
  report.steps.push({ step: "adminLogin", ok: !!token, status: login.status });
  if (!token) {
    writeFileSync(join(OUT, "prompt3-rbac-repair.json"), JSON.stringify(report, null, 2));
    console.log(JSON.stringify(report, null, 2));
    return;
  }

  const before = extractPermissions(token);
  report.before = {
    permissionCount: before.length,
    hasAllocationRun: before.includes("Allocation.Run"),
    allocationKeys: before.filter((p) => p.startsWith("Allocation")).sort(),
  };

  const roles = await api(token, "/tenant-rbac/roles");
  const roleList = Array.isArray(roles.body) ? roles.body : roles.body?.items || [];
  const adminRole = roleList.find((r) => String(r.code || r.Code || "").toUpperCase() === "ADMIN");
  report.steps.push({
    step: "findAdminRole",
    ok: !!adminRole,
    roleId: adminRole?.id || adminRole?.Id || null,
  });
  if (!adminRole) {
    writeFileSync(join(OUT, "prompt3-rbac-repair.json"), JSON.stringify(report, null, 2));
    console.log(JSON.stringify(report, null, 2));
    return;
  }

  const roleId = adminRole.id || adminRole.Id;
  const detail = await api(token, `/tenant-rbac/roles/${roleId}`);
  const currentIds = detail.body?.permissionIds || detail.body?.PermissionIds || [];
  const catalog = await api(token, "/tenant-rbac/permissions");
  const allPerms = Array.isArray(catalog.body) ? catalog.body : catalog.body?.items || [];
  const byKey = new Map(allPerms.map((p) => [p.key || p.Key, p.id || p.Id]));

  const neededIds = ALLOCATION_KEYS.map((k) => byKey.get(k)).filter((id) => id != null);
  const merged = [...new Set([...currentIds, ...neededIds])].sort((a, b) => a - b);

  report.steps.push({
    step: "mergePermissionIds",
    beforeCount: currentIds.length,
    afterCount: merged.length,
    addedAllocationIds: neededIds.filter((id) => !currentIds.includes(id)),
  });

  const put = await api(token, `/tenant-rbac/roles/${roleId}/permissions`, {
    method: "PUT",
    body: JSON.stringify({ permissionIds: merged }),
  });
  report.steps.push({ step: "setRolePermissions", ok: put.ok, status: put.status });

  // Re-login to refresh JWT
  const login2 = await api(null, "/auth/login", {
    method: "POST",
    body: JSON.stringify({
      universityCode: "001",
      collegeCode: "1053",
      username: "admin",
      password: "admin123",
    }),
  });
  const token2 = login2.body?.token || login2.body?.accessToken;
  const after = token2 ? extractPermissions(token2) : [];
  report.after = {
    permissionCount: after.length,
    hasAllocationRun: after.includes("Allocation.Run"),
    allocationKeys: after.filter((p) => p.startsWith("Allocation")).sort(),
  };

  // Probe simulate
  let sim = { status: null };
  if (token2) {
    sim = await api(token2, "/allocation/simulate", {
      method: "POST",
      body: JSON.stringify({
        academicYearId: 1,
        courseId: 1,
        groupId: 2,
        semesterId: 3,
        groupingMode: "LastThreeDigits",
        enabledStrategies: { Validation: true, Capacity: true },
        populationSelection: { mode: "AllEligible" },
        targetSectionIds: null,
      }),
    });
  }
  report.simulateProbe = {
    status: sim.status,
    ok: sim.ok,
    recommendationCount: (sim.body?.recommendations || sim.body?.Recommendations || []).length,
    scenarioIdPresent: !!(sim.body?.scenarioId || sim.body?.ScenarioId),
  };

  // Faculty unauthorized probe
  const facLogin = await api(null, "/auth/login", {
    method: "POST",
    body: JSON.stringify({
      universityCode: "001",
      collegeCode: "1053",
      username: "teststaff1",
      password: "teststaff1f",
    }),
  });
  const facToken = facLogin.body?.token || facLogin.body?.accessToken;
  let facSim = { status: null, hasAllocationRun: false };
  if (facToken) {
    const facPerms = extractPermissions(facToken);
    facSim.hasAllocationRun = facPerms.includes("Allocation.Run");
    const r = await api(facToken, "/allocation/simulate", {
      method: "POST",
      body: JSON.stringify({
        academicYearId: 1,
        courseId: 1,
        groupId: 2,
        semesterId: 3,
        groupingMode: "Alphabetical",
        enabledStrategies: { Capacity: true },
        populationSelection: { mode: "AllEligible" },
      }),
    });
    facSim.status = r.status;
    facSim.denied = r.status === 403;
  } else {
    facSim.loginFailed = true;
    facSim.status = facLogin.status;
  }
  report.facultyUnauthorized = facSim;

  report.authorization =
    report.after.hasAllocationRun && report.simulateProbe.ok && facSim.denied !== false
      ? facSim.denied || facSim.loginFailed
        ? "PASS"
        : "FAIL — faculty not denied"
      : "FAIL";

  writeFileSync(join(OUT, "prompt3-rbac-repair.json"), JSON.stringify(report, null, 2));
  console.log(
    JSON.stringify(
      {
        beforeRun: report.before.hasAllocationRun,
        afterRun: report.after.hasAllocationRun,
        allocationKeysAfter: report.after.allocationKeys,
        simulate: report.simulateProbe,
        faculty: report.facultyUnauthorized,
        authorization: report.authorization,
      },
      null,
      2,
    ),
  );
}

main().catch((e) => {
  console.error(String(e?.stack || e));
  process.exit(1);
});
