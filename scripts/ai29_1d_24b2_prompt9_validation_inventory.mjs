/**
 * AI29.1D.24B.2 Prompt 9.4 — validation data inventory (read-only classification).
 * Does NOT delete or restore. Writes classification report for human-governed cleanup.
 */
import { writeFileSync, mkdirSync, readFileSync, existsSync } from "node:fs";
import { join } from "node:path";

const API = process.env.API_URL || "http://localhost:5210/api";
const OUT =
  process.env.OUT_DIR ||
  join("D:", "Resheta", "AttendenceProject", "CursonModifiedFiles", "AI Attandance", "AI29.1D.24B.2", "Prompt 9");

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

async function login() {
  const r = await api(null, "/auth/login", {
    method: "POST",
    body: JSON.stringify({
      universityCode: "001",
      collegeCode: "1053",
      username: "admin",
      password: "admin123",
    }),
  });
  return r.body?.token || r.body?.accessToken || null;
}

function asList(body) {
  if (Array.isArray(body)) return body;
  if (Array.isArray(body?.items)) return body.items;
  if (Array.isArray(body?.data)) return body.data;
  return [];
}

function loadJson(p) {
  if (!existsSync(p)) return null;
  try {
    return JSON.parse(readFileSync(p, "utf8"));
  } catch {
    return null;
  }
}

async function main() {
  mkdirSync(OUT, { recursive: true });
  const token = await login();
  const items = [];

  const push = (row) => items.push(row);

  // Prior inventories
  const p7inv = loadJson(join("scripts", "ai29_1d_24b2_prompt7_inventory.json"));
  const p7art = loadJson(
    join(
      "D:",
      "Resheta",
      "AttendenceProject",
      "CursonModifiedFiles",
      "AI Attandance",
      "AI29.1D.24B.2",
      "Prompt 7",
      "validation-inventory.json",
    ),
  );
  const p8mem = loadJson(
    join(
      "D:",
      "Resheta",
      "AttendenceProject",
      "CursonModifiedFiles",
      "AI Attandance",
      "AI29.1D.24B.2",
      "Prompt 8",
      "section-membership-prep.json",
    ),
  );
  const p8persona = loadJson(
    join(
      "D:",
      "Resheta",
      "AttendenceProject",
      "CursonModifiedFiles",
      "AI Attandance",
      "AI29.1D.24B.2",
      "Prompt 8",
      "persona-prep.json",
    ),
  );

  push({
    id: "sem-IV",
    description: "Semester IV (id=9) under B.Com / Computer Applications",
    origin: "Prompt 7 — created when missing for Test F",
    classification: "B",
    action: "RETAIN — legitimate academic catalog node; do not delete semester",
    evidence: p7inv?.semesterIV || null,
  });

  push({
    id: "section-CA-IV-A",
    description: "Section CA-IV-A under Semester IV",
    origin: "Prompt 7 — created for Test F",
    classification: "B",
    action: "RETAIN — no safe delete while Sem IV students may reference it; not deleted",
  });

  const createdSections = [
    ...(p7art?.sections?.ca || []).filter((s) => s.created),
    ...(p7art?.sections?.finance || []).filter((s) => s.created),
  ];
  for (const s of createdSections) {
    push({
      id: `section-${s.code}`,
      description: `Section ${s.code} (id=${s.id})`,
      origin: "Prompt 7 data prep (created:true)",
      classification: "B",
      action: "RETAIN — temporary validation section; deletion deferred (memberships / operational reuse risk)",
    });
  }

  push({
    id: "section-SCCA01",
    description: "Section SCCA01",
    origin: "Pre-existing (created:false in Prompt 7 inventory)",
    classification: "A",
    action: "RETAIN — existing legitimate data",
  });

  push({
    id: "sem-IV-students",
    description: "Five B.Com/CA students reassigned to Semester IV",
    origin: "Prompt 7 — population for workflow continue (Student update API)",
    classification: "C",
    action:
      "NO RESTORE — Original academic assignment could not be established; restoration not performed.",
    evidenceNote:
      "No before/after student id + original semesterId map was persisted in Prompt 7 inventory or scripts.",
  });

  push({
    id: "student-sections-CA-A-B",
    description: "20 StudentSections on CA-A + 20 on CA-B",
    origin: "Prompt 8 section-membership-prep",
    classification: "B",
    action: "RETAIN for acceptance re-run of optional Section attendance; not deleted in Prompt 9",
    evidence: {
      assignedA: p8mem?.assignedA,
      assignedB: p8mem?.assignedB,
    },
  });

  push({
    id: "faculty-teststaff1",
    description: "User teststaff1 / staffId 7 Faculty-A persona",
    origin: "Prompt 8 persona prep (may pre-exist; password/roles/subjects mutated)",
    classification: "C",
    action: "RETAIN persona; password churn is operational; no delete",
    evidence: p8persona?.facultyA || null,
  });

  push({
    id: "faculty-role-101-section-view",
    description: "FACULTY application role (101) permission id 210 Section.View",
    origin: "Prompt 8 — required for Section selector authorization",
    classification: "A",
    action: "RETAIN — correct tenant RBAC configuration, not temporary junk data",
  });

  push({
    id: "staff-7-subjects",
    description: "Staff 7 teaching subject assignments for B.Com/CA/Sem III",
    origin: "Prompt 8 persona prep",
    classification: "C",
    action: "RETAIN — original subject set not inventoried; no restore guess",
  });

  // Live snapshot
  let live = {};
  if (token) {
    const secsIII = await api(
      token,
      "/sections?academicYearId=1&courseId=1&groupId=2&semesterId=3",
    );
    const secsIV = await api(
      token,
      "/sections?academicYearId=1&courseId=1&groupId=2&semesterId=9",
    );
    const memA = await api(token, "/student-sections?sectionId=4&pageSize=1");
    const memB = await api(token, "/student-sections?sectionId=5&pageSize=1");
    live = {
      caIII: asList(secsIII.body).map((s) => ({ id: s.id, code: s.code || s.sectionCode, name: s.name })),
      caIV: asList(secsIV.body).map((s) => ({ id: s.id, code: s.code || s.sectionCode, name: s.name })),
      caA_membershipTotal: memA.body?.total ?? memA.body?.totalCount ?? asList(memA.body).length,
      caB_membershipTotal: memB.body?.total ?? memB.body?.totalCount ?? asList(memB.body).length,
      caA_http: memA.status,
      caB_http: memB.status,
    };
  }

  const report = {
    date: new Date().toISOString(),
    mode: "inventory-classify-only",
    classificationLegend: {
      A: "Existing legitimate data",
      B: "Temporary validation data",
      C: "Existing data modified temporarily",
      D: "Unknown / cannot safely determine",
    },
    items,
    liveSnapshot: live,
    cleanupExecuted: [],
    restoreExecuted: [],
    intentionallyRetained: items.filter((i) => String(i.action).startsWith("RETAIN")).map((i) => i.id),
    notRestored: items.filter((i) => String(i.action).includes("NO RESTORE")).map((i) => i.id),
  };

  writeFileSync(join(OUT, "prompt9-validation-inventory.json"), JSON.stringify(report, null, 2));
  console.log(
    JSON.stringify(
      {
        itemCount: items.length,
        intentionallyRetained: report.intentionallyRetained,
        notRestored: report.notRestored,
        liveSnapshot: live,
      },
      null,
      2,
    ),
  );
}

main().catch((e) => {
  console.error(String(e?.stack || e));
  process.exit(1);
});
