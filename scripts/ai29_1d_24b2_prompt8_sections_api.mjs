/**
 * Probe sections list for Faculty-A scope (no secrets printed).
 */
const API = process.env.API_URL || "http://localhost:5210/api";

async function login(username, password) {
  const res = await fetch(`${API}/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ universityCode: "001", collegeCode: "1053", username, password }),
  });
  const body = await res.json().catch(() => ({}));
  return { ok: res.ok, token: body.token, mustChangePassword: !!body.mustChangePassword };
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

const admin = await login("admin", "admin123");
const a = await login("teststaff1", "teststaff1f");
console.log({ adminOk: admin.ok, facultyOk: a.ok, facultyMustChange: a.mustChangePassword });

const token = a.ok ? a.token : admin.token;
const courses = asList((await api(token, "/master/courses")).body);
const course = courses.find((c) => /b\.?\s*com/i.test(String(c.name || ""))) || courses[0];
const groups = asList((await api(token, `/master/groups?courseId=${course.id}`)).body);
const group = groups.find((g) => /computer/i.test(String(g.name || ""))) || groups[0];
const semesters = asList((await api(token, `/master/semesters?courseId=${course.id}&groupId=${group.id}`)).body);
const semester = semesters.find((s) => /III|3/i.test(String(s.name || ""))) || semesters[0];
console.log({ courseId: course?.id, groupId: group?.id, semesterId: semester?.id, semesterName: semester?.name });

for (const path of [
  `/sections?courseId=${course.id}&groupId=${group.id}&semesterId=${semester.id}`,
  `/sections?academicYearId=1&courseId=${course.id}&groupId=${group.id}&semesterId=${semester.id}`,
  `/master/sections?courseId=${course.id}&groupId=${group.id}&semesterId=${semester.id}`,
  `/master/sections?academicYearId=1&courseId=${course.id}&groupId=${group.id}&semesterId=${semester.id}`,
  `/academic/sections?courseId=${course.id}&groupId=${group.id}&semesterId=${semester.id}&academicYearId=1`,
]) {
  const r = await api(token, path);
  const list = asList(r.body);
  console.log(path, r.status, "count", list.length, "sample", list.slice(0, 3).map((s) => s.sectionCode || s.code || s.name || s.id));
}

// admin probe same
if (admin.ok) {
  const r = await api(
    admin.token,
    `/sections?academicYearId=1&courseId=${course.id}&groupId=${group.id}&semesterId=${semester.id}`,
  );
  console.log("admin sections", r.status, asList(r.body).length, asList(r.body).slice(0, 5).map((s) => s.sectionCode || s.code));
}
