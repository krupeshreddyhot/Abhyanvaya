/** Probe section-scoped students-for-marking as Faculty-A (counts/status only). */
const API = process.env.API_URL || "http://localhost:5210/api";

async function login(username, password) {
  const res = await fetch(`${API}/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ universityCode: "001", collegeCode: "1053", username, password }),
  });
  return res.json();
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
  return { status: res.status, body };
}

const fac = await login("teststaff1", "teststaff1f");
const token = fac.token;
const sections = (await api(token, "/sections?academicYearId=1&courseId=1&groupId=2&semesterId=3")).body || [];
console.log(
  "sections",
  sections.map((s) => ({ id: s.id, code: s.sectionCode, name: s.sectionName })),
);
const subjects = (await api(token, "/master/subjects?courseId=1&groupId=2&semesterId=3")).body || [];
const subjectId = subjects[0]?.id;
console.log("subjectId", subjectId, subjects[0]?.name);

for (const s of sections) {
  const q = new URLSearchParams({
    courseId: "1",
    groupId: "2",
    semesterId: "3",
    subjectId: String(subjectId),
    date: "2026-08-11",
    pageNumber: "1",
    pageSize: "50",
    sectionIds: String(s.id),
  });
  const r = await api(token, `/attendance/students-for-marking?${q}`);
  const total = r.body?.totalCount ?? r.body?.data?.totalCount ?? null;
  const count = (r.body?.students || r.body?.data?.students || []).length;
  console.log({ code: s.sectionCode, status: r.status, total, pageCount: count });
}

// unscoped
const q2 = new URLSearchParams({
  courseId: "1",
  groupId: "2",
  semesterId: "3",
  subjectId: String(subjectId),
  date: "2026-08-11",
  pageNumber: "1",
  pageSize: "50",
});
const all = await api(token, `/attendance/students-for-marking?${q2}`);
console.log("unscoped", all.status, all.body?.totalCount ?? all.body?.data?.totalCount);
