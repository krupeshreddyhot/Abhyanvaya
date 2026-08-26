/**
 * AI29.1D.24B.2 Prompt 9.1 / 9.5 — Discovery only (no mutations).
 * No passwords or tokens printed.
 */
import { writeFileSync, mkdirSync, readFileSync, existsSync } from "node:fs";
import { join } from "node:path";

const API = process.env.API_URL || "http://localhost:5210/api";
const OUT =
  process.env.OUT_DIR ||
  join("D:", "Resheta", "AttendenceProject", "CursonModifiedFiles", "AI Attandance", "AI29.1D.24B.2", "Prompt 9");

async function login(username, password) {
  const res = await fetch(`${API}/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ universityCode: "001", collegeCode: "1053", username, password }),
  });
  const body = await res.json().catch(() => ({}));
  return { ok: res.ok, status: res.status, token: body.token || null };
}

async function api(token, path) {
  const res = await fetch(`${API}${path}`, { headers: { Authorization: `Bearer ${token}` } });
  const text = await res.text();
  let body = null;
  try {
    body = text ? JSON.parse(text) : null;
  } catch {
    body = text?.slice?.(0, 200) || text;
  }
  return { ok: res.ok, status: res.status, body };
}

function asList(x) {
  if (Array.isArray(x)) return x;
  if (x?.items) return x.items;
  if (x?.data && Array.isArray(x.data)) return x.data;
  return [];
}

function claimFlags(token) {
  if (!token) return null;
  const payload = JSON.parse(Buffer.from(token.split(".")[1], "base64url").toString("utf8"));
  const raw = payload.permission;
  const list = Array.isArray(raw) ? raw : raw ? [raw] : [];
  return {
    permissionCount: list.length,
    hasSectionView: list.includes("Section.View"),
    hasAttendanceView: list.includes("Attendance.View"),
    hasAttendanceManage: list.includes("Attendance.Manage"),
    hasProgramManage: list.includes("Program.Manage"),
    tenantId: Number(payload.TenantId),
    role: payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] || payload.role || null,
    staffId: Number(payload.StaffId || 0),
  };
}

mkdirSync(OUT, { recursive: true });

const report = {
  date: new Date().toISOString(),
  mode: "discovery-only",
  personas: {},
  sectionGroups: {},
  timetable: {},
  validationArtifacts: {},
  notes: [],
};

const admin = await login("admin", "admin123");
report.personas.admin = { loginOk: admin.ok, status: admin.status };
if (!admin.ok) {
  report.notes.push("Admin login failed; discovery incomplete");
  writeFileSync(join(OUT, "prompt9-discovery.json"), JSON.stringify(report, null, 2));
  process.exit(1);
}

const facA = await login("teststaff1", "teststaff1f");
const facB = await login("knraj", "knraj123");
report.personas.facultyA = {
  username: "teststaff1",
  loginOk: facA.ok,
  status: facA.status,
  claims: facA.ok ? claimFlags(facA.token) : null,
};
report.personas.facultyB = {
  username: "knraj",
  loginOk: facB.ok,
  status: facB.status,
  claims: facB.ok ? claimFlags(facB.token) : null,
};

if (facA.ok) {
  const r = await api(facA.token, "/attendance-resolution/current");
  report.personas.facultyA.resolver = {
    status: r.status,
    mode: r.body?.mode,
    hasTimetable: r.body?.hasTimetable,
    message: (r.body?.message || "").slice(0, 160),
  };
  report.personas.facultyA.sectionsHttp = (
    await api(facA.token, "/sections?academicYearId=1&courseId=1&groupId=2&semesterId=3")
  ).status;
}

if (facB.ok) {
  const dates = [];
  const today = new Date();
  for (let i = -3; i <= 14; i++) {
    const d = new Date(today);
    d.setDate(today.getDate() + i);
    dates.push(d.toISOString().slice(0, 10));
  }
  const probes = [];
  for (const date of dates) {
    const r = await api(facB.token, `/attendance-resolution/current?date=${date}`);
    probes.push({
      date,
      status: r.status,
      mode: r.body?.mode,
      hasTimetable: r.body?.hasTimetable,
      isCombined: r.body?.isCombinedClass ?? r.body?.IsCombinedClass ?? null,
      sectionIds: r.body?.participatingSectionIds || r.body?.sectionIds || null,
    });
    if (r.body?.hasTimetable === true) break;
  }
  report.personas.facultyB.resolverProbes = probes;
  report.personas.facultyB.anyTimetable = probes.some((p) => p.hasTimetable === true);
}

const groups = asList((await api(admin.token, "/section-groups")).body);
report.sectionGroups = {
  count: groups.length,
  items: groups.slice(0, 20).map((g) => ({
    id: g.id,
    name: g.name || g.code,
    sectionIds: g.sectionIds || g.memberSectionIds || g.sections || null,
  })),
  combinedCandidates: groups.filter((g) => {
    const members = g.sectionIds || g.memberSectionIds || g.sections || [];
    return (Array.isArray(members) ? members.length : 0) >= 2;
  }).length,
};

// Timetable discovery via admin endpoints (best-effort)
const ttHits = [];
for (const path of [
  "/scheduling/timetables?pageSize=50",
  "/timetables?pageSize=50",
  "/scheduling/timetable?pageSize=50",
]) {
  const r = await api(admin.token, path);
  if (r.status !== 404) {
    const list = asList(r.body);
    ttHits.push({
      path,
      status: r.status,
      count: list.length,
      sample: list.slice(0, 5).map((t) => ({
        id: t.id,
        status: t.status || t.Status,
        facultyId: t.facultyId || t.staffId,
        academicYearId: t.academicYearId,
      })),
    });
  }
}
report.timetable.adminListHits = ttHits;

const sections = asList(
  (await api(admin.token, "/sections?academicYearId=1&courseId=1&groupId=2&semesterId=3")).body,
);
report.validationArtifacts.sectionsCA_III = sections.map((s) => ({
  id: s.id,
  code: s.sectionCode,
  name: s.sectionName,
}));

for (const code of ["CA-A", "CA-B"]) {
  const sec = sections.find((s) => s.sectionCode === code);
  if (!sec) continue;
  const ss = asList((await api(admin.token, `/student-sections?sectionId=${sec.id}&currentOnly=true`)).body);
  report.validationArtifacts[`studentSections_${code}`] = { sectionId: sec.id, currentCount: ss.length };
}

// Prior inventory files
const priorPaths = [
  join("scripts", "ai29_1d_24b2_prompt7_inventory.json"),
  join(
    "D:",
    "Resheta",
    "AttendenceProject",
    "CursonModifiedFiles",
    "AI Attandance",
    "AI29.1D.24B.2",
    "Prompt 7",
    "validation-inventory.json",
  ),
  join(
    "D:",
    "Resheta",
    "AttendenceProject",
    "CursonModifiedFiles",
    "AI Attandance",
    "AI29.1D.24B.2",
    "Prompt 8",
    "section-membership-prep.json",
  ),
  join(
    "D:",
    "Resheta",
    "AttendenceProject",
    "CursonModifiedFiles",
    "AI Attandance",
    "AI29.1D.24B.2",
    "Prompt 8",
    "persona-prep.json",
  ),
];
report.priorInventories = {};
for (const p of priorPaths) {
  if (existsSync(p)) {
    try {
      report.priorInventories[p] = JSON.parse(readFileSync(p, "utf8"));
    } catch {
      report.priorInventories[p] = { parseError: true };
    }
  }
}

const users = asList((await api(admin.token, "/tenant-rbac/users")).body);
const uA = users.find((u) => String(u.username || "").toLowerCase() === "teststaff1");
const uB = users.find((u) => String(u.username || "").toLowerCase() === "knraj");
report.personas.facultyA.rbac = uA
  ? { userId: uA.id, staffId: uA.staffId, applicationRoleIds: uA.applicationRoleIds }
  : null;
report.personas.facultyB.rbac = uB
  ? { userId: uB.id, staffId: uB.staffId, applicationRoleIds: uB.applicationRoleIds }
  : null;

if (uA?.applicationRoleIds?.[0]) {
  const role = await api(admin.token, `/tenant-rbac/roles/${uA.applicationRoleIds[0]}`);
  report.personas.facultyA.roleDetail = {
    id: role.body?.id,
    code: role.body?.code,
    permissionIds: role.body?.permissionIds,
    hasSectionViewId210: (role.body?.permissionIds || []).includes(210),
  };
}

writeFileSync(join(OUT, "prompt9-discovery.json"), JSON.stringify(report, null, 2));
console.log(
  JSON.stringify(
    {
      facultyA: report.personas.facultyA,
      facultyB: {
        loginOk: report.personas.facultyB.loginOk,
        anyTimetable: report.personas.facultyB.anyTimetable,
        claims: report.personas.facultyB.claims,
      },
      sectionGroups: report.sectionGroups,
      timetableHits: report.timetable.adminListHits,
      caMembership: {
        A: report.validationArtifacts.studentSections_CA_A,
        B: report.validationArtifacts.studentSections_CA_B,
      },
    },
    null,
    2,
  ),
);
