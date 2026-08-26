/**
 * AI29.1D.24B.2 Prompt 7 — validation data preparation (admin APIs only).
 * Does not print tokens. Writes inventory JSON for browser harness.
 */
import { writeFileSync, mkdirSync } from "node:fs";
import { join } from "node:path";

const API = process.env.API_URL || "http://localhost:5210/api";
const OUT =
  process.env.OUT_DIR ||
  join("D:", "Resheta", "AttendenceProject", "CursonModifiedFiles", "AI Attandance", "AI29.1D.24B.2", "Prompt 7");

const inventory = {
  ok: false,
  academicYear: null,
  programCommerce: null,
  programOther: null,
  courseBCom: null,
  groupCA: null,
  groupFinance: null,
  semesterIII: null,
  semesterIV: null,
  sections: { ca: [], finance: [], semIV: [] },
  emptyScope: null,
  facultyNoTimetable: null,
  notes: [],
};

async function loginCollege() {
  const res = await fetch(`${API}/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      universityCode: "001",
      collegeCode: "1053",
      username: "admin",
      password: "admin123",
    }),
  });
  if (!res.ok) throw new Error(`college login HTTP ${res.status}`);
  const json = await res.json();
  const token = json.token || json.accessToken || json.Token;
  if (!token) throw new Error("no token");
  return token;
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
  if (x?.items && Array.isArray(x.items)) return x.items;
  if (x?.data && Array.isArray(x.data)) return x.data;
  return [];
}

function matchName(list, re) {
  return list.find((x) => re.test(String(x.name || x.code || x.label || "")));
}

async function ensureSection(token, scope, code, name, order) {
  const listRes = await api(
    token,
    `/sections?academicYearId=${scope.academicYearId}&courseId=${scope.courseId}&groupId=${scope.groupId}&semesterId=${scope.semesterId}`,
  );
  const existing = asList(listRes.body).find(
    (s) => String(s.sectionCode || s.code || "").toUpperCase() === code.toUpperCase(),
  );
  if (existing) return { created: false, section: existing };

  const create = await api(token, "/sections", {
    method: "POST",
    body: JSON.stringify({
      academicYearId: scope.academicYearId,
      courseId: scope.courseId,
      groupId: scope.groupId,
      semesterId: scope.semesterId,
      sectionCode: code,
      sectionName: name,
      displayOrder: order,
      maximumStrength: 60,
      status: "Active",
      sectionTypeCode: "Regular",
      minimumCapacity: 10,
      recommendedCapacity: 40,
    }),
  });
  if (!create.ok) {
    inventory.notes.push(`create ${code} failed HTTP ${create.status}: ${JSON.stringify(create.body)}`);
    return { created: false, section: null, error: create };
  }
  return { created: true, section: create.body };
}

async function main() {
  mkdirSync(OUT, { recursive: true });
  const token = await loginCollege();

  const years = asList((await api(token, "/scheduling/academic-years")).body);
  const year = matchName(years, /2026/) || years[0];
  if (!year?.id) throw new Error("academic year missing");
  inventory.academicYear = { id: year.id, name: year.name || year.code };

  const programs = asList((await api(token, "/programs")).body);
  const commerce = matchName(programs, /commerce/i);
  const otherProg = programs.find((p) => p.id !== commerce?.id && /active|true|1/i.test(String(p.isActive ?? p.status ?? "active")));
  inventory.programCommerce = commerce ? { id: commerce.id, name: commerce.name || commerce.code } : null;
  inventory.programOther = otherProg ? { id: otherProg.id, name: otherProg.name || otherProg.code } : null;
  if (!inventory.programOther) inventory.notes.push("Only one Program available — Test G may be NOT EXECUTED");

  const courses = asList((await api(token, "/course")).body);
  const bcom = matchName(courses, /b\.?\s*com/i);
  if (!bcom?.id) throw new Error("B.Com course missing");
  inventory.courseBCom = { id: bcom.id, name: bcom.name || bcom.code, programId: bcom.programId };

  const groups = asList((await api(token, `/group?courseId=${bcom.id}`)).body);
  const ca = matchName(groups, /computer applications/i);
  const finance = matchName(groups, /finance/i);
  if (!ca?.id || !finance?.id) throw new Error("CA/Finance groups missing");
  inventory.groupCA = { id: ca.id, name: ca.name || ca.code };
  inventory.groupFinance = { id: finance.id, name: finance.name || finance.code };

  const semCA = asList((await api(token, `/semester?courseId=${bcom.id}&groupId=${ca.id}`)).body);
  const semIII = matchName(semCA, /\bIII\b|^3$|semester\s*3/i) || semCA.find((s) => /iii/i.test(String(s.name)));
  const semIV = matchName(semCA, /\bIV\b|^4$|semester\s*4/i) || semCA.find((s) => /iv/i.test(String(s.name)));
  if (!semIII?.id) throw new Error("Semester III missing for CA");
  inventory.semesterIII = { id: semIII.id, name: semIII.name || semIII.code };
  inventory.semesterIV = semIV ? { id: semIV.id, name: semIV.name || semIV.code } : null;
  if (!semIV) inventory.notes.push("Semester IV missing for CA — Test F may be NOT EXECUTED unless created via UI/admin");

  const caScope = {
    academicYearId: year.id,
    courseId: bcom.id,
    groupId: ca.id,
    semesterId: semIII.id,
  };
  const financeScope = {
    academicYearId: year.id,
    courseId: bcom.id,
    groupId: finance.id,
    semesterId: semIII.id,
  };

  // Ensure CA sections (A + B)
  for (const [code, name, order] of [
    ["CA-A", "Computer Applications A", 1],
    ["CA-B", "Computer Applications B", 2],
    ["SCCA01", "Computer Applications Section 01", 3],
  ]) {
    const r = await ensureSection(token, caScope, code, name, order);
    if (r.section) inventory.sections.ca.push({ id: r.section.id || r.section.sectionId, code: r.section.sectionCode || code, created: r.created });
  }

  // Finance section
  {
    const r = await ensureSection(token, financeScope, "FIN-A", "Finance A", 1);
    if (r.section)
      inventory.sections.finance.push({
        id: r.section.id || r.section.sectionId,
        code: r.section.sectionCode || "FIN-A",
        created: r.created,
      });
  }

  // Semester IV section for CA if semester exists
  if (semIV?.id) {
    const r = await ensureSection(
      token,
      { ...caScope, semesterId: semIV.id },
      "CA-IV-A",
      "Computer Applications IV-A",
      1,
    );
    if (r.section)
      inventory.sections.semIV.push({
        id: r.section.id || r.section.sectionId,
        code: r.section.sectionCode || "CA-IV-A",
        created: r.created,
      });
  }

  // Empty scope for Test H: find semester with zero sections under CA, or create a dedicated semester if API allows
  let emptyFound = null;
  for (const s of semCA) {
    const listRes = await api(
      token,
      `/sections?academicYearId=${year.id}&courseId=${bcom.id}&groupId=${ca.id}&semesterId=${s.id}`,
    );
    const secs = asList(listRes.body);
    if (secs.length === 0) {
      emptyFound = { academicYearId: year.id, courseId: bcom.id, groupId: ca.id, semesterId: s.id, semesterName: s.name };
      break;
    }
  }
  if (!emptyFound && finance?.id) {
    const semFin = asList((await api(token, `/semester?courseId=${bcom.id}&groupId=${finance.id}`)).body);
    for (const s of semFin) {
      const listRes = await api(
        token,
        `/sections?academicYearId=${year.id}&courseId=${bcom.id}&groupId=${finance.id}&semesterId=${s.id}`,
      );
      if (asList(listRes.body).length === 0) {
        emptyFound = {
          academicYearId: year.id,
          courseId: bcom.id,
          groupId: finance.id,
          semesterId: s.id,
          semesterName: s.name,
          groupName: "Finance",
        };
        break;
      }
    }
  }
  inventory.emptyScope = emptyFound;
  if (!emptyFound) {
    // Try create a new semester for empty-scope testing via existing semester API if available
    const createSem = await api(token, "/semester", {
      method: "POST",
      body: JSON.stringify({
        courseId: bcom.id,
        groupId: ca.id,
        name: "Validation Empty Scope",
        code: "VAL-EMPTY",
        order: 99,
        displayOrder: 99,
      }),
    });
    if (createSem.ok && (createSem.body?.id || createSem.body?.semesterId)) {
      const sid = createSem.body.id || createSem.body.semesterId;
      inventory.emptyScope = {
        academicYearId: year.id,
        courseId: bcom.id,
        groupId: ca.id,
        semesterId: sid,
        semesterName: createSem.body.name || "Validation Empty Scope",
        created: true,
      };
      inventory.notes.push("Created Validation Empty Scope semester for Test H (no Section attached)");
    } else {
      inventory.notes.push(
        `Test H empty scope unavailable; semester create HTTP ${createSem.status}: ${JSON.stringify(createSem.body)}`,
      );
    }
  }

  // Faculty without timetable — list tenant users / faculty
  const users = asList((await api(token, "/tenant-users")).body);
  const facultyUsers = users.filter((u) => /faculty/i.test(String(u.role || u.roleName || u.userType || "")));
  let facultyCandidate = null;
  for (const f of facultyUsers.slice(0, 20)) {
    const id = f.id || f.userId || f.facultyId;
    // If timetable list exists, skip those with assignments
    const tt = await api(token, `/timetable?facultyId=${id}`).catch(() => ({ ok: false }));
    const ttList = tt.ok ? asList(tt.body) : [];
    if (ttList.length === 0) {
      facultyCandidate = { id, username: f.username || f.userName || f.email, note: "no timetable rows from /timetable?facultyId" };
      break;
    }
  }
  inventory.facultyNoTimetable = facultyCandidate;
  if (!facultyCandidate)
    inventory.notes.push(
      "No faculty-without-timetable persona confirmed via API; Test I may be NOT EXECUTED unless credentials are provided",
    );

  inventory.ok = true;
  const outPath = join(OUT, "validation-inventory.json");
  writeFileSync(outPath, JSON.stringify(inventory, null, 2));
  writeFileSync(join(process.cwd(), "scripts", "ai29_1d_24b2_prompt7_inventory.json"), JSON.stringify(inventory, null, 2));
  console.log(JSON.stringify({ ok: inventory.ok, notes: inventory.notes, summary: {
    year: inventory.academicYear,
    caSections: inventory.sections.ca.map((s) => s.code),
    financeSections: inventory.sections.finance.map((s) => s.code),
    semIVSections: inventory.sections.semIV.map((s) => s.code),
    emptyScope: inventory.emptyScope,
    programOther: inventory.programOther,
    facultyNoTimetable: inventory.facultyNoTimetable ? { id: inventory.facultyNoTimetable.id, username: inventory.facultyNoTimetable.username } : null,
  }}, null, 2));
}

main().catch((e) => {
  console.error("DATA_PREP_FAIL", e.message || e);
  process.exit(1);
});
