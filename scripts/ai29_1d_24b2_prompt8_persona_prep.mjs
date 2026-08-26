/**
 * AI29.1D.24B.2 Prompt 8.2 — Prepare Faculty-A validation persona (dev/validation only).
 * Uses admin credentials from env (defaults match user-provided validation login).
 * Does not print passwords or tokens.
 */
import { writeFileSync, mkdirSync } from "node:fs";
import { join } from "node:path";

const API = process.env.API_URL || "http://localhost:5210/api";
const OUT =
  process.env.OUT_DIR ||
  join("D:", "Resheta", "AttendenceProject", "CursonModifiedFiles", "AI Attandance", "AI29.1D.24B.2", "Prompt 8");

const ADMIN_USER = process.env.ADMIN_USER || "admin";
const ADMIN_PASS = process.env.ADMIN_PASS || "admin123";
const FACULTY_A_USER = process.env.FACULTY_A_USER || "teststaff1";
const FACULTY_A_PASS = process.env.FACULTY_A_PASS || "teststaff1f";

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
  return { ok: res.ok, status: res.status, token: body.token || null, mustChangePassword: !!body.mustChangePassword };
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
  return [];
}

async function main() {
  mkdirSync(OUT, { recursive: true });
  const report = { steps: [], facultyA: {}, notes: [] };

  const admin = await login(ADMIN_USER, ADMIN_PASS);
  if (!admin.ok || !admin.token) {
    report.steps.push({ step: "adminLogin", status: "FAIL", detail: `status=${admin.status}` });
    writeFileSync(join(OUT, "persona-prep.json"), JSON.stringify(report, null, 2));
    process.exit(1);
  }
  report.steps.push({ step: "adminLogin", status: "PASS" });
  const token = admin.token;

  const staffRes = await api(token, "/staff?pageSize=100");
  const staff = asList(staffRes.body);
  report.steps.push({ step: "listStaff", status: staffRes.ok ? "PASS" : "FAIL", count: staff.length });

  // Load subject ids via Course→Group→Semester cascade (master/subjects)
  const courses = asList((await api(token, "/master/courses")).body);
  const course =
    courses.find((c) => /b\.?\s*com/i.test(String(c.name || c.code || ""))) || courses[0];
  const courseId = course?.id || course?.courseId;
  const groups = asList((await api(token, `/master/groups?courseId=${courseId}`)).body);
  const group =
    groups.find((g) => /computer/i.test(String(g.name || g.code || ""))) || groups[0];
  const groupId = group?.id || group?.groupId;
  const semesters = asList(
    (await api(token, `/master/semesters?courseId=${courseId}&groupId=${groupId}`)).body,
  );
  const semester =
    semesters.find((s) => /\bIII\b|Semester\s*III|3/i.test(String(s.name || s.code || s.number || ""))) ||
    semesters[0];
  const semesterId = semester?.id || semester?.semesterId;
  const subjects = asList(
    (
      await api(
        token,
        `/master/subjects?courseId=${courseId}&groupId=${groupId}&semesterId=${semesterId}`,
      )
    ).body,
  );
  let subjectIds = subjects.map((s) => s.id || s.Id).filter(Boolean).slice(0, 5);
  report.steps.push({
    step: "resolveCatalogSubjects",
    status: subjectIds.length > 0 ? "PASS" : "FAIL",
    courseId,
    groupId,
    semesterId,
    subjectCount: subjectIds.length,
  });

  // Enrich staff with detail subjectIds
  const enriched = [];
  for (const s of staff) {
    const id = s.id || s.staffId;
    const detail = await api(token, `/staff/${id}`);
    const subjectIdsOnStaff = asList(detail.body?.subjectIds || detail.body?.SubjectIds || s.subjectIds).map(Number);
    enriched.push({
      id,
      firstName: s.firstName || detail.body?.firstName,
      lastName: s.lastName || detail.body?.lastName,
      subjectIds: subjectIdsOnStaff,
    });
  }
  const withSubjects = enriched.filter((s) => s.subjectIds.length > 0);
  let targetStaff =
    withSubjects.find((s) => !/raj/i.test(`${s.firstName || ""} ${s.lastName || ""}`)) ||
    withSubjects[0] ||
    enriched[0];

  if (!targetStaff) {
    report.notes.push("No staff records available");
    writeFileSync(join(OUT, "persona-prep.json"), JSON.stringify(report, null, 2));
    process.exit(1);
  }

  const staffId = targetStaff.id;
  // Merge catalog Sem III subjects with any existing teaching assignments so manual cascade can load roster.
  const mergedSubjectIds = Array.from(
    new Set([...targetStaff.subjectIds, ...subjectIds].map(Number).filter((n) => n > 0)),
  );
  if (mergedSubjectIds.length === 0) {
    report.steps.push({ step: "assignSubjects", status: "FAIL", detail: "No subjects found to assign" });
  } else {
    const detail = await api(token, `/staff/${staffId}`);
    const body = detail.body || {};
    const putPayload = {
      collegeId: body.collegeId,
      staffCode: body.staffCode,
      staffTypeId: body.staffTypeId,
      personTitleId: body.personTitleId,
      designationId: body.designationId,
      qualificationId: body.qualificationId,
      genderId: body.genderId,
      employmentStatusId: body.employmentStatusId,
      firstName: body.firstName,
      lastName: body.lastName,
      phone: body.phone,
      altPhone: body.altPhone,
      email: body.email,
      website: body.website,
      dateOfJoining: body.dateOfJoining,
      contractEndDate: body.contractEndDate,
      dateOfBirth: body.dateOfBirth,
      departments: body.departments,
      collegeRoleLookupIds: body.collegeRoleLookupIds || body.collegeRoles?.map((r) => r.id || r),
      subjectIds: mergedSubjectIds,
    };
    const put = await api(token, `/staff/${staffId}`, {
      method: "PUT",
      body: JSON.stringify(putPayload),
    });
    subjectIds = mergedSubjectIds;
    report.steps.push({
      step: "assignSubjects",
      status: put.ok ? "PASS" : "FAIL",
      staffId,
      subjectCount: mergedSubjectIds.length,
      http: put.status,
      detail: typeof put.body === "string" ? put.body.slice(0, 160) : put.body,
    });
  }

  // Find Faculty-A via tenant RBAC user list, then reset password to known validation value
  let facultyLogin = await login(FACULTY_A_USER, FACULTY_A_PASS);
  let userId = null;
  const rbacUsers = asList((await api(token, "/tenant-rbac/users")).body);
  const existing = rbacUsers.find(
    (x) => String(x.userName || x.username || x.Username || "").toLowerCase() === FACULTY_A_USER,
  );
  if (existing) {
    userId = existing.id || existing.userId || existing.Id;
    report.steps.push({ step: "findUser", status: "PASS", userId, staffId: existing.staffId });
  }

  if (!facultyLogin.ok) {
    if (!userId) {
      const create = await api(token, "/tenant-users", {
        method: "POST",
        body: JSON.stringify({
          username: FACULTY_A_USER,
          password: FACULTY_A_PASS,
          role: "Faculty",
          staffId,
        }),
      });
      report.steps.push({
        step: "createFacultyA",
        status: create.ok
          ? "PASS"
          : create.status === 400 && /already in use/i.test(String(create.body))
            ? "EXISTS"
            : "FAIL",
        http: create.status,
        detail: typeof create.body === "string" ? create.body.slice(0, 160) : create.body,
      });
      if (create.ok && create.body?.id) userId = create.body.id;
    }

    if (userId) {
      const reset = await api(token, `/tenant-users/${userId}/reset-password`, {
        method: "POST",
        body: JSON.stringify({ newPassword: FACULTY_A_PASS }),
      });
      report.steps.push({ step: "resetPassword", status: reset.ok ? "PASS" : "FAIL", http: reset.status, userId });
      // Ensure linked to staff with subjects
      const link = await api(token, `/tenant-users/${userId}/staff`, {
        method: "PUT",
        body: JSON.stringify({ staffId }),
      });
      report.steps.push({ step: "linkStaff", status: link.ok ? "PASS" : "FAIL", http: link.status, staffId });
    }

    facultyLogin = await login(FACULTY_A_USER, FACULTY_A_PASS);
  } else {
    report.steps.push({ step: "facultyALogin", status: "PASS", detail: "existing password works" });
    if (userId) {
      const link = await api(token, `/tenant-users/${userId}/staff`, {
        method: "PUT",
        body: JSON.stringify({ staffId }),
      });
      report.steps.push({ step: "linkStaff", status: link.ok ? "PASS" : "FAIL", http: link.status, staffId });
    }
  }

  report.steps.push({
    step: "facultyALoginFinal",
    status: facultyLogin.ok ? "PASS" : "FAIL",
    http: facultyLogin.status,
    mustChangePassword: facultyLogin.mustChangePassword,
  });

  // Ensure Faculty-A has SectionView so optional Section cascade can list sections (existing RBAC API).
  if (userId) {
    const perms = asList((await api(token, "/tenant-rbac/permissions")).body);
    const sectionView = perms.find((p) =>
      /section\.view|sectionview|sections\.view/i.test(String(p.key || p.Key || "")),
    );
    const roles = asList((await api(token, "/tenant-rbac/roles")).body);
    let facultyRole =
      roles.find((r) => /faculty/i.test(String(r.code || r.name || ""))) ||
      roles.find((r) => /attendance/i.test(String(r.code || r.name || "")));

    if (!facultyRole && sectionView) {
      const created = await api(token, "/tenant-rbac/roles", {
        method: "POST",
        body: JSON.stringify({
          name: "Faculty Attendance Validation",
          code: "FACULTY_ATTENDANCE_VALIDATION",
          description: "AI29.1D.24B.2 Prompt 8 validation role",
        }),
      });
      if (created.ok && created.body?.id) {
        facultyRole = { id: created.body.id, code: "FACULTY_ATTENDANCE_VALIDATION" };
        report.steps.push({ step: "createValidationRole", status: "PASS", roleId: facultyRole.id });
      } else {
        report.steps.push({
          step: "createValidationRole",
          status: "FAIL",
          http: created.status,
          detail: created.body,
        });
      }
    }

    if (facultyRole?.id) {
      const detail = await api(token, `/tenant-rbac/roles/${facultyRole.id}`);
      const existingPermIds = asList(detail.body?.permissionIds || detail.body?.PermissionIds).map(Number);
      const needed = new Set(existingPermIds);
      if (sectionView?.id) needed.add(Number(sectionView.id));
      // Keep any attendance-related permissions if present in catalog
      for (const p of perms) {
        if (/attendance/i.test(String(p.key || ""))) needed.add(Number(p.id));
      }
      const putPerms = await api(token, `/tenant-rbac/roles/${facultyRole.id}/permissions`, {
        method: "PUT",
        body: JSON.stringify({ permissionIds: Array.from(needed) }),
      });
      report.steps.push({
        step: "rolePermissions",
        status: putPerms.ok ? "PASS" : "FAIL",
        roleId: facultyRole.id,
        permissionCount: needed.size,
        http: putPerms.status,
      });

      const users = asList((await api(token, "/tenant-rbac/users")).body);
      const row = users.find((x) => (x.id || x.userId) === userId);
      const roleIds = new Set(asList(row?.applicationRoleIds).map(Number));
      roleIds.add(Number(facultyRole.id));
      const putRoles = await api(token, `/tenant-rbac/users/${userId}/application-roles`, {
        method: "PUT",
        body: JSON.stringify({ applicationRoleIds: Array.from(roleIds) }),
      });
      report.steps.push({
        step: "assignUserRoles",
        status: putRoles.ok ? "PASS" : "FAIL",
        userId,
        roleIds: Array.from(roleIds),
        http: putRoles.status,
      });
    } else {
      report.steps.push({
        step: "rolePermissions",
        status: "FAIL",
        detail: "No Faculty application role and could not create validation role",
      });
    }
  }

  // Re-login so JWT picks up new roles/permissions
  facultyLogin = await login(FACULTY_A_USER, FACULTY_A_PASS);

  if (facultyLogin.ok && facultyLogin.token) {
    const resolve = await api(facultyLogin.token, "/attendance-resolution/current");
    const sectionsProbe = await api(
      facultyLogin.token,
      `/sections?academicYearId=1&courseId=${courseId}&groupId=${groupId}&semesterId=${semesterId}`,
    );
    report.facultyA = {
      username: FACULTY_A_USER,
      loginOk: true,
      staffId,
      mode: resolve.body?.mode,
      hasTimetable: resolve.body?.hasTimetable,
      subjectIdsAssigned: subjectIds.length,
      sectionsListStatus: sectionsProbe.status,
      sectionsCount: asList(sectionsProbe.body).length,
    };
    report.steps.push({
      step: "resolveNoTimetable",
      status: resolve.ok && resolve.body?.hasTimetable === false ? "PASS" : "FAIL",
      mode: resolve.body?.mode,
      hasTimetable: resolve.body?.hasTimetable,
    });
    report.steps.push({
      step: "sectionsListAsFaculty",
      status: sectionsProbe.ok && asList(sectionsProbe.body).length > 0 ? "PASS" : "FAIL",
      http: sectionsProbe.status,
      count: asList(sectionsProbe.body).length,
    });
  } else {
    report.facultyA = { username: FACULTY_A_USER, loginOk: false };
  }

  writeFileSync(join(OUT, "persona-prep.json"), JSON.stringify(report, null, 2));
  console.log(JSON.stringify({ facultyA: report.facultyA, steps: report.steps }, null, 2));
  if (!facultyLogin.ok) process.exit(1);
}

main().catch((e) => {
  console.error(String(e.message || e));
  process.exit(1);
});
