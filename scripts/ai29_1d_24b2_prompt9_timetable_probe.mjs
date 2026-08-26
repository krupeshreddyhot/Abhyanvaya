/**
 * AI29.1D.24B.2 Prompt 9.5 — timetable / SectionGroup probe (read-only).
 * Does not create timetable or combined-section data.
 */
import { writeFileSync, mkdirSync } from "node:fs";
import { join } from "node:path";

const API = process.env.API_URL || "http://localhost:5210/api";
const OUT =
  process.env.OUT_DIR ||
  join("D:", "Resheta", "AttendenceProject", "CursonModifiedFiles", "AI Attandance", "AI29.1D.24B.2", "Prompt 9");

const STATUS = { 1: "Draft", 2: "Locked", 3: "Published", 4: "Archived" };

async function api(token, path, opts = {}) {
  const res = await fetch(`${API}${path}`, {
    ...opts,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(opts.headers || {}),
    },
  });
  let body = null;
  const text = await res.text();
  try {
    body = text ? JSON.parse(text) : null;
  } catch {
    body = text;
  }
  return { ok: res.ok, status: res.status, body };
}

async function login(username, password) {
  const r = await api(null, "/auth/login", {
    method: "POST",
    body: JSON.stringify({
      universityCode: "001",
      collegeCode: "1053",
      username,
      password,
    }),
  });
  const token = r.body?.token || r.body?.accessToken || null;
  return { ...r, token };
}

function asList(body) {
  if (Array.isArray(body)) return body;
  if (Array.isArray(body?.items)) return body.items;
  if (Array.isArray(body?.data)) return body.data;
  if (Array.isArray(body?.results)) return body.results;
  return [];
}

async function main() {
  mkdirSync(OUT, { recursive: true });
  const report = {
    date: new Date().toISOString(),
    mode: "read-only-probe",
    timetableStatusEnum: STATUS,
    admin: {},
    facultyB: {},
    sectionGroups: {},
    verdict: {},
  };

  const admin = await login("admin", "admin123");
  report.admin.loginOk = !!admin.token;
  if (!admin.token) {
    writeFileSync(join(OUT, "prompt9-timetable-probe.json"), JSON.stringify(report, null, 2));
    console.log(JSON.stringify(report, null, 2));
    return;
  }

  const ttList = await api(admin.token, "/scheduling/timetables?pageSize=100");
  const timetables = asList(ttList.body);
  report.admin.timetableListHttp = ttList.status;
  report.admin.timetables = timetables.map((t) => ({
    id: t.id,
    status: t.status,
    statusName: STATUS[t.status] || String(t.status),
    academicYearId: t.academicYearId,
    name: t.name || t.title || null,
    isPublishedOrLocked: t.status === 2 || t.status === 3,
  }));

  const details = [];
  for (const t of timetables.slice(0, 10)) {
    const d = await api(admin.token, `/scheduling/timetables/${t.id}`);
    const body = d.body || {};
    const entries = asList(body.entries || body.slots || body.periods || body.timetableEntries);
    const sections = asList(body.timetableSections || body.sections);
    details.push({
      id: t.id,
      http: d.status,
      status: body.status ?? t.status,
      statusName: STATUS[body.status ?? t.status] || null,
      entryCount: entries.length,
      timetableSectionCount: sections.length,
      sampleEntryKeys: entries[0] ? Object.keys(entries[0]).slice(0, 12) : [],
    });
  }
  report.admin.timetableDetails = details;

  const sg = await api(admin.token, "/section-groups?pageSize=100");
  const groups = asList(sg.body);
  report.sectionGroups = {
    http: sg.status,
    count: groups.length,
    items: groups.map((g) => ({
      id: g.id,
      name: g.name || g.code,
      sectionIds: g.sectionIds || g.sections?.map((s) => s.id || s.sectionId) || [],
    })),
    combinedCandidates: groups.filter((g) => {
      const ids = g.sectionIds || g.sections?.map((s) => s.id || s.sectionId) || [];
      return Array.isArray(ids) && ids.length >= 2;
    }).length,
  };

  const facB = await login("knraj", "knraj123");
  report.facultyB.loginOk = !!facB.token;
  if (facB.token) {
    const probes = [];
    for (let i = 0; i < 14; i++) {
      const d = new Date();
      d.setUTCDate(d.getUTCDate() + i);
      const iso = d.toISOString().slice(0, 10);
      const r = await api(facB.token, `/attendance-resolution/current?date=${iso}`);
      probes.push({
        date: iso,
        status: r.status,
        mode: r.body?.mode,
        hasTimetable: r.body?.hasTimetable,
        isCombined: r.body?.isCombined ?? r.body?.combined,
        sectionIds: r.body?.sectionIds || r.body?.participatingSectionIds || [],
        message: r.body?.message || null,
      });
    }
    report.facultyB.resolverProbes = probes;
    report.facultyB.anyPublishedTimetableSession = probes.some((p) => p.hasTimetable === true);
  }

  const publishedOrLocked = report.admin.timetables.filter((t) => t.isPublishedOrLocked);
  report.verdict = {
    hasAnyTimetableRecord: timetables.length > 0,
    hasPublishedOrLockedTimetable: publishedOrLocked.length > 0,
    publishedOrLockedIds: publishedOrLocked.map((t) => t.id),
    hasSectionGroup: groups.length > 0,
    hasCombinedSectionGroup: report.sectionGroups.combinedCandidates > 0,
    facultyBHasResolverTimetable: report.facultyB.anyPublishedTimetableSession === true,
    test3_timetableAttendance: publishedOrLocked.length > 0 && report.facultyB.anyPublishedTimetableSession
      ? "EXECUTABLE"
      : "NOT EXECUTED — DATA UNAVAILABLE",
    test4_combinedAB:
      report.sectionGroups.combinedCandidates > 0 && report.facultyB.anyPublishedTimetableSession
        ? "EXECUTABLE"
        : "NOT EXECUTED — DATA UNAVAILABLE",
  };

  writeFileSync(join(OUT, "prompt9-timetable-probe.json"), JSON.stringify(report, null, 2));
  console.log(JSON.stringify({ verdict: report.verdict, timetables: report.admin.timetables, sectionGroups: report.sectionGroups }, null, 2));
}

main().catch((e) => {
  console.error(String(e?.stack || e));
  process.exit(1);
});
