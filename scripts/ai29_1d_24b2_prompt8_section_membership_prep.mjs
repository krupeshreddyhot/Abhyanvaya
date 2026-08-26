/**
 * Prompt 8 validation data: assign Sem III students into CA-A / CA-B via existing admin APIs.
 * Does not invent resolvers; does not print secrets.
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
    body: JSON.stringify({ universityCode: "001", collegeCode: "1053", username, password }),
  });
  const body = await res.json().catch(() => ({}));
  return { ok: res.ok, token: body.token };
}

async function api(token, path, opts = {}) {
  const res = await fetch(`${API}${path}`, {
    ...opts,
    headers: {
      Authorization: `Bearer ${token}`,
      "Content-Type": "application/json",
      ...(opts.headers || {}),
    },
  });
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
  if (x?.students && Array.isArray(x.students)) return x.students;
  return [];
}

const report = { assignedA: 0, assignedB: 0, errors: [], notes: [] };
mkdirSync(OUT, { recursive: true });

const admin = await login("admin", "admin123");
if (!admin.ok) {
  console.error(JSON.stringify({ ok: false, step: "adminLogin" }));
  process.exit(1);
}
const token = admin.token;

const sections = asList((await api(token, "/sections?academicYearId=1&courseId=1&groupId=2&semesterId=3")).body);
const caA = sections.find((s) => s.sectionCode === "CA-A");
const caB = sections.find((s) => s.sectionCode === "CA-B");
if (!caA || !caB) {
  report.notes.push("CA-A/CA-B not found");
  writeFileSync(join(OUT, "section-membership-prep.json"), JSON.stringify(report, null, 2));
  process.exit(1);
}

// Prefer students-for-marking unscoped via a Faculty token would need subject; use students list
let students = [];
for (const path of [
  "/students?courseId=1&groupId=2&semesterId=3&pageSize=100",
  "/students?pageSize=100",
  "/master/students?courseId=1&groupId=2&semesterId=3",
]) {
  const r = await api(token, path);
  const list = asList(r.body);
  if (r.ok && list.length) {
    students = list;
    report.notes.push(`students from ${path}: ${list.length}`);
    break;
  }
}

// Fallback: attendance students-for-marking needs subject — use first subject as admin if available
if (students.length === 0) {
  const subjects = asList((await api(token, "/master/subjects?courseId=1&groupId=2&semesterId=3")).body);
  const sid = subjects[0]?.id;
  if (sid) {
    const r = await api(
      token,
      `/attendance/students-for-marking?courseId=1&groupId=2&semesterId=3&subjectId=${sid}&date=2026-08-11&pageNumber=1&pageSize=100`,
    );
    students = asList(r.body?.students || r.body);
    report.notes.push(`students from attendance roster: ${students.length}`);
  }
}

// Attendance roster returns studentNumber only — resolve numeric Student.Id via /student list
const rosterNumbers = students
  .map((s) => s.studentNumber || s.StudentNumber)
  .filter(Boolean)
  .slice(0, 40);

let dirList = [];
for (let page = 1; page <= 10 && dirList.length < 400; page++) {
  const r = await api(token, `/student?courseId=1&groupId=2&semesterId=3&pageNumber=${page}&pageSize=100`);
  const chunk = asList(r.body);
  if (!r.ok || chunk.length === 0) break;
  dirList.push(...chunk);
}
report.notes.push(`directory students: ${dirList.length}; rosterNumbers: ${rosterNumbers.length}`);

const numberToId = new Map();
for (const s of dirList) {
  const num = String(s.studentNumber || s.StudentNumber || s.admissionNumber || "");
  const id = s.id || s.studentId;
  if (num && id) numberToId.set(num, Number(id));
}

let ids = rosterNumbers.map((n) => numberToId.get(String(n))).filter((n) => Number(n) > 0);
report.notes.push(`resolved ids: ${ids.length}`);
if (ids.length === 0 && dirList.length > 0) {
  // last resort: assign first directory students for C/G/S if fields exist
  const scoped = dirList.filter((s) => {
    const c = s.courseId || s.CourseId;
    const g = s.groupId || s.GroupId;
    const sem = s.semesterId || s.SemesterId;
    return (!c || c === 1) && (!g || g === 2) && (!sem || sem === 3);
  });
  ids = scoped.map((s) => s.id || s.studentId).filter(Boolean).slice(0, 40);
  report.notes.push(`fallback directory ids: ${ids.length}`);
}

const half = Math.ceil(ids.length / 2);
const toA = ids.slice(0, half);
const toB = ids.slice(half);

async function assignMany(sectionId, studentIds, label) {
  let ok = 0;
  for (const studentId of studentIds) {
    const r = await api(token, "/student-sections", {
      method: "POST",
      body: JSON.stringify({ studentId, sectionId, effectiveFrom: "2025-06-01" }),
    });
    if (r.ok) ok++;
    else if (/already|exists|current/i.test(String(r.body))) ok++;
    else report.errors.push({ label, studentId, status: r.status, body: String(r.body).slice(0, 120) });
  }
  return ok;
}

report.assignedA = await assignMany(caA.id, toA, "CA-A");
report.assignedB = await assignMany(caB.id, toB, "CA-B");

// Verify via students-for-marking as admin
const subjects = asList((await api(token, "/master/subjects?courseId=1&groupId=2&semesterId=3")).body);
const subjectId = subjects[0]?.id;
for (const s of [caA, caB]) {
  const r = await api(
    token,
    `/attendance/students-for-marking?courseId=1&groupId=2&semesterId=3&subjectId=${subjectId}&date=2026-08-11&pageNumber=1&pageSize=50&sectionIds=${s.id}`,
  );
  report[`verify_${s.sectionCode}`] = {
    status: r.status,
    total: r.body?.totalCount ?? null,
  };
}

writeFileSync(join(OUT, "section-membership-prep.json"), JSON.stringify(report, null, 2));
console.log(JSON.stringify(report, null, 2));
if (report.assignedA + report.assignedB === 0) process.exit(1);
