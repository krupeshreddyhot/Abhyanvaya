/**
 * AI29.1D.24B.3A — Apply least-privilege ADMIN Allocation permissions via tenant-rbac API.
 * Ensures operator set; removes governance/ops over-grants from ADMIN.
 * Does not print tokens/passwords.
 */
import { writeFileSync, mkdirSync } from "node:fs";
import { join } from "node:path";

const API = process.env.API_URL || "http://localhost:5210/api";
const OUT =
  process.env.OUT_DIR ||
  join("D:", "Resheta", "AttendenceProject", "CursonModifiedFiles", "AI Attandance", "AI29.1D.24B.3A", "Prompt 3");

const OPERATOR_KEYS = [
  "Allocation.Run",
  "Allocation.Scenario.View",
  "Allocation.Scenario.Create",
  "Allocation.Scenario.Compare",
  "Allocation.Scenario.Replay",
  "Allocation.Scenario.Review",
];
const GOVERNANCE_OPS_KEYS = [
  "Allocation.Approve",
  "Allocation.Operations.View",
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
    body = text?.slice?.(0, 300) ?? text;
  }
  return { ok: res.ok, status: res.status, body };
}

function extractPermissions(token) {
  const raw = Buffer.from(token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/"), "base64").toString("utf8");
  const payload = JSON.parse(raw);
  return Array.isArray(payload.permission) ? payload.permission.map(String) : [];
}

async function main() {
  mkdirSync(OUT, { recursive: true });
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
  if (!token) {
    console.log(JSON.stringify({ error: "login failed", status: login.status }));
    return;
  }

  const before = extractPermissions(token);
  const roles = await api(token, "/tenant-rbac/roles");
  const roleList = Array.isArray(roles.body) ? roles.body : [];
  const admin = roleList.find((r) => String(r.code || "").toUpperCase() === "ADMIN");
  if (!admin) {
    console.log(JSON.stringify({ error: "ADMIN role not found" }));
    return;
  }

  const detail = await api(token, `/tenant-rbac/roles/${admin.id}`);
  const currentIds = detail.body?.permissionIds || [];
  const catalog = await api(token, "/tenant-rbac/permissions");
  const all = Array.isArray(catalog.body) ? catalog.body : [];
  const byKey = new Map(all.map((p) => [p.key, p.id]));
  const byId = new Map(all.map((p) => [p.id, p.key]));

  const govIds = new Set(GOVERNANCE_OPS_KEYS.map((k) => byKey.get(k)).filter((x) => x != null));
  const opIds = OPERATOR_KEYS.map((k) => byKey.get(k)).filter((x) => x != null);

  const withoutGov = currentIds.filter((id) => !govIds.has(id));
  const merged = [...new Set([...withoutGov, ...opIds])].sort((a, b) => a - b);

  const put = await api(token, `/tenant-rbac/roles/${admin.id}/permissions`, {
    method: "PUT",
    body: JSON.stringify({ permissionIds: merged }),
  });

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
  const allocAfter = after.filter((p) => p.startsWith("Allocation")).sort();

  const sim = token2
    ? await api(token2, "/allocation/simulate", {
        method: "POST",
        body: JSON.stringify({
          academicYearId: 1,
          courseId: 1,
          groupId: 2,
          semesterId: 3,
          groupingMode: "LastThreeDigits",
          enabledStrategies: { Capacity: true, Validation: true },
          populationSelection: { mode: "AllEligible" },
        }),
      })
    : { status: null };

  const approve = token2
    ? await api(token2, `/allocation/scenarios/${sim.body?.scenarioId || "00000000-0000-0000-0000-000000000000"}/approve`, {
        method: "POST",
        body: "{}",
      })
    : { status: null };

  const fac = await api(null, "/auth/login", {
    method: "POST",
    body: JSON.stringify({
      universityCode: "001",
      collegeCode: "1053",
      username: "teststaff1",
      password: "teststaff1f",
    }),
  });
  const facToken = fac.body?.token || fac.body?.accessToken;
  const facSim = facToken
    ? await api(facToken, "/allocation/simulate", {
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
      })
    : { status: fac.status };

  const report = {
    putStatus: put.status,
    beforeAlloc: before.filter((p) => p.startsWith("Allocation")).sort(),
    afterAlloc: allocAfter,
    hasRun: after.includes("Allocation.Run"),
    hasApprove: after.includes("Allocation.Approve"),
    hasOpsView: after.includes("Allocation.Operations.View"),
    hasReject: after.includes("Allocation.Reject"),
    hasArchive: after.includes("Allocation.Scenario.Archive"),
    hasExport: after.includes("Allocation.Export"),
    operatorAllPresent: OPERATOR_KEYS.every((k) => after.includes(k)),
    governanceAbsent: GOVERNANCE_OPS_KEYS.every((k) => !after.includes(k)),
    simulate: { status: sim.status, ok: sim.ok, scenario: !!(sim.body?.scenarioId) },
    approveAttempt: { status: approve.status },
    facultySimulate: { status: facSim.status, denied: facSim.status === 403 },
    removedIds: currentIds.filter((id) => !merged.includes(id)).map((id) => byId.get(id) || id),
    addedIds: merged.filter((id) => !currentIds.includes(id)).map((id) => byId.get(id) || id),
  };

  writeFileSync(join(OUT, "prompt3a-least-privilege-apply.json"), JSON.stringify(report, null, 2));
  console.log(JSON.stringify(report, null, 2));
}

main().catch((e) => {
  console.error(String(e?.stack || e));
  process.exit(1);
});
