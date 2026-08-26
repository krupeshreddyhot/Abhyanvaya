/** Probe whether Faculty-A token authorizes sections (status only; no claim dump). */
const API = process.env.API_URL || "http://localhost:5210/api";

async function login(username, password) {
  const res = await fetch(`${API}/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ universityCode: "001", collegeCode: "1053", username, password }),
  });
  return res.json();
}

async function status(token, path) {
  const res = await fetch(`${API}${path}`, { headers: { Authorization: `Bearer ${token}` } });
  return res.status;
}

const admin = await login("admin", "admin123");
const faculty = await login("teststaff1", "teststaff1f");
const kn = await login("knraj", "knraj123");

const path = "/sections?academicYearId=1&courseId=1&groupId=2&semesterId=3";
console.log({
  adminSections: await status(admin.token, path),
  facultySections: await status(faculty.token, path),
  knrajSections: await status(kn.token, path),
  facultyMasterCourses: await status(faculty.token, "/master/courses"),
  facultyAttendanceStudents: await status(
    faculty.token,
    "/attendance/students-for-marking?courseId=1&groupId=2&semesterId=3&subjectId=1&date=2026-08-11&pageNumber=1&pageSize=10",
  ),
});
