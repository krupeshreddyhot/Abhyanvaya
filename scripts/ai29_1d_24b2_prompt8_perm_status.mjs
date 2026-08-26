/**
 * Status-only permission probe: which Faculty-gated endpoints succeed after RBAC assign.
 * Does not print tokens or claim payloads.
 */
const API = process.env.API_URL || "http://localhost:5210/api";

async function login(username, password) {
  const res = await fetch(`${API}/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ universityCode: "001", collegeCode: "1053", username, password }),
  });
  const body = await res.json();
  return { status: res.status, token: body.token };
}

async function st(token, path) {
  const res = await fetch(`${API}${path}`, { headers: { Authorization: `Bearer ${token}` } });
  return res.status;
}

const admin = await login("admin", "admin123");
const users = await (
  await fetch(`${API}/tenant-rbac/users`, { headers: { Authorization: `Bearer ${admin.token}` } })
).json();
const u = users.find((x) => String(x.username).toLowerCase() === "teststaff1");
console.log("userRoles", u?.applicationRoleIds);

const role = await (
  await fetch(`${API}/tenant-rbac/roles/101`, { headers: { Authorization: `Bearer ${admin.token}` } })
).json();
console.log("role101 has Section.View id 210?", (role.permissionIds || []).includes(210), "count", (role.permissionIds || []).length);

const faculty = await login("teststaff1", "teststaff1f");
const paths = [
  "/master/courses",
  "/sections?academicYearId=1&courseId=1&groupId=2&semesterId=3",
  "/faculty-sections",
  "/attendance-resolution/current",
  "/programs",
];
const out = {};
for (const p of paths) out[p] = await st(faculty.token, p);
console.log(out);
