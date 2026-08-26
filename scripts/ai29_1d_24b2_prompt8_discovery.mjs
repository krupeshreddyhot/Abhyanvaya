/**
 * AI29.1D.24B.2 Prompt 8.1 — Faculty/timetable/SectionGroup discovery.
 * Does not print tokens or passwords.
 */
import { writeFileSync, mkdirSync } from "node:fs";
import { join } from "node:path";

const API = process.env.API_URL || "http://localhost:5210/api";
const OUT =
  process.env.OUT_DIR ||
  join("D:", "Resheta", "AttendenceProject", "CursonModifiedFiles", "AI Attandance", "AI29.1D.24B.2", "Prompt 8");

async function login(username, password) {
  const res = await fetch(`${API}/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      universityCode: "001",
      collegeCode: "1053",
      username,
      password,
    }),
  });
  const body = await res.json().catch(() => ({}));
  return {
    ok: res.ok,
    status: res.status,
    token: body.token || body.accessToken || body.Token || null,
    role: body.role || body.userRole || body.Role || null,
    permissions: body.permissions || body.Permissions || null,
    userId: body.userId || body.UserId || body.id || null,
    staffId: body.staffId || body.StaffId || null,
  };
}

async function api(token, path) {
  const res = await fetch(`${API}${path}`, { headers: { Authorization: `Bearer ${token}` } });
  const text = await res.text();
  let body = null;
  try {
    body = text ? JSON.parse(text) : null;
  } catch {
    body = text;
  }
  return { ok: res.ok, status: res.status, body };
}

function asList(x) {
  if (Array.isArray(x)) return x;
  if (x?.items) return x.items;
  if (x?.data && Array.isArray(x.data)) return x.data;
  return [];
}

async function probeFaculty(label, username, password) {
  const auth = await login(username, password);
  if (!auth.ok || !auth.token) {
    return { label, username, loginOk: false, status: auth.status };
  }
  const token = auth.token;
  const me = await api(token, "/auth/me").catch(() => ({ ok: false }));
  const perms = asList(auth.permissions || me.body?.permissions);
  const hasAttendance =
    perms.some((p) => /attendance/i.test(String(p?.key || p?.name || p))) ||
    /faculty|admin/i.test(String(auth.role || me.body?.role || ""));

  const timetablePaths = [
    "/timetable/today",
    "/timetable/my",
    "/faculty-workspace/timetable/today",
    "/scheduling/timetable/today",
    "/attendance/session/today",
    "/attendance/resolve-today",
  ];
  const timetableHits = [];
  for (const p of timetablePaths) {
    const r = await api(token, p);
    if (r.status !== 404) {
      const list = asList(r.body);
      timetableHits.push({
        path: p,
        status: r.status,
        ok: r.ok,
        count: Array.isArray(list) ? list.length : r.body ? 1 : 0,
        sampleKeys: r.body && typeof r.body === "object" ? Object.keys(r.body).slice(0, 12) : [],
      });
    }
  }

  // Session resolver style endpoints used by UI
  const resolvePaths = [
    "/scheduling/attendance-session/resolve",
    "/attendance/session/resolve",
  ];
  for (const p of resolvePaths) {
    const r = await api(token, `${p}?date=${new Date().toISOString().slice(0, 10)}`);
    if (r.status !== 404) {
      timetableHits.push({ path: p, status: r.status, ok: r.ok, keys: r.body ? Object.keys(r.body).slice(0, 15) : [] });
    }
  }

  return {
    label,
    username,
    loginOk: true,
    role: auth.role || me.body?.role || null,
    userId: auth.userId || me.body?.userId || null,
    staffId: auth.staffId || me.body?.staffId || null,
    hasAttendanceSignal: hasAttendance,
    permissionCount: perms.length,
    timetableHits,
  };
}

async function adminDiscovery(adminToken) {
  const groups = asList((await api(adminToken, "/section-groups")).body);
  const combined = groups.filter((g) => {
    const members = g.sectionIds || g.memberSectionIds || g.sections || [];
    return (Array.isArray(members) ? members.length : 0) >= 2 || /combined|\+/i.test(String(g.name || g.code || ""));
  });
  return {
    sectionGroupCount: groups.length,
    combinedCandidates: combined.slice(0, 10).map((g) => ({
      id: g.id || g.sectionGroupId,
      code: g.code || g.sectionGroupCode,
      name: g.name,
      sectionIds: g.sectionIds || g.memberSectionIds || null,
    })),
  };
}

async function main() {
  mkdirSync(OUT, { recursive: true });
  const report = {
    date: new Date().toISOString(),
    facultyA: await probeFaculty("FACULTY-A", "teststaff1", "teststaff1f"),
    facultyB: await probeFaculty("FACULTY-B", "knraj", "knraj123"),
    admin: null,
    sectionGroups: null,
    notes: [],
  };

  const adminAuth = await login("admin", "admin123");
  if (adminAuth.ok && adminAuth.token) {
    report.admin = { loginOk: true, role: adminAuth.role };
    report.sectionGroups = await adminDiscovery(adminAuth.token);
  } else {
    report.admin = { loginOk: false, status: adminAuth.status };
    report.notes.push("Admin login failed for SectionGroup discovery");
  }

  if (!report.facultyA.loginOk) report.notes.push("FACULTY-A login failed");
  if (!report.facultyB.loginOk) report.notes.push("FACULTY-B login failed");

  writeFileSync(join(OUT, "prompt8-discovery.json"), JSON.stringify(report, null, 2));
  writeFileSync(join(process.cwd(), "scripts", "ai29_1d_24b2_prompt8_discovery.json"), JSON.stringify(report, null, 2));
  console.log(
    JSON.stringify(
      {
        facultyA: {
          loginOk: report.facultyA.loginOk,
          role: report.facultyA.role,
          timetableHits: report.facultyA.timetableHits?.map((h) => `${h.path}:${h.status}:${h.count ?? ""}`),
        },
        facultyB: {
          loginOk: report.facultyB.loginOk,
          role: report.facultyB.role,
          timetableHits: report.facultyB.timetableHits?.map((h) => `${h.path}:${h.status}:${h.count ?? ""}`),
        },
        sectionGroups: report.sectionGroups,
        notes: report.notes,
      },
      null,
      2,
    ),
  );
}

main().catch((e) => {
  console.error("DISCOVERY_FAIL", e.message || e);
  process.exit(1);
});
