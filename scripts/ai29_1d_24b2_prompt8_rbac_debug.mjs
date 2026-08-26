const API = process.env.API_URL || "http://localhost:5210/api";

async function login(username, password) {
  const res = await fetch(`${API}/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ universityCode: "001", collegeCode: "1053", username, password }),
  });
  const body = await res.json().catch(() => ({}));
  return body;
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

const admin = await login("admin", "admin123");
const role = await api(admin.token, "/tenant-rbac/roles/101");
console.log("role101", JSON.stringify(role.body, null, 2)?.slice(0, 1500));
const perms = await api(admin.token, "/tenant-rbac/permissions");
const list = Array.isArray(perms.body) ? perms.body : [];
const sectionish = list.filter((p) => /section/i.test(String(p.key || "")));
console.log(
  "section perms",
  sectionish.map((p) => ({ id: p.id, key: p.key })),
);
const faculty = await login("teststaff1", "teststaff1f");
const me = await api(faculty.token, "/auth/me");
console.log("me keys", me.body && Object.keys(me.body));
console.log(
  "me perms sample",
  JSON.stringify(me.body?.permissions || me.body?.Permissions || me.body?.claims || null)?.slice(0, 800),
);
const sec = await api(faculty.token, "/sections?academicYearId=1&courseId=1&groupId=2&semesterId=3");
console.log("sections", sec.status, Array.isArray(sec.body) ? sec.body.length : sec.body);
