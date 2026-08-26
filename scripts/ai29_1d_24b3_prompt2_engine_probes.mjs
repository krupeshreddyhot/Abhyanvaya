import { writeFileSync, readFileSync, mkdirSync } from "node:fs";
import { join } from "node:path";

const API = process.env.API_URL || "http://localhost:5210/api";
const OUT =
  process.env.OUT_DIR ||
  join("D:", "Resheta", "AttendenceProject", "CursonModifiedFiles", "AI Attandance", "AI29.1D.24B.3", "Prompt 2");

function extractPermissions(token) {
  const raw = Buffer.from(token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/"), "base64").toString("utf8");
  const perms = [...raw.matchAll(/"permission"\s*:\s*"([^"]+)"/g)].map((m) => m[1]);
  return { rawLength: raw.length, permissions: [...new Set(perms)].sort() };
}

async function main() {
  const login = await fetch(`${API}/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      universityCode: "001",
      collegeCode: "1053",
      username: "admin",
      password: "admin123",
    }),
  });
  const body = await login.json();
  const token = body.token || body.accessToken;
  const { permissions } = extractPermissions(token);
  const allocation = permissions.filter((p) => p.startsWith("Allocation"));
  const summary = {
    permissionCount: permissions.length,
    hasAllocationRun: permissions.includes("Allocation.Run"),
    hasAllocationApprove: permissions.includes("Allocation.Approve"),
    hasAllocationOpsView: permissions.includes("Allocation.Operations.View"),
    hasSectionView: permissions.includes("Section.View"),
    hasAllocationTest: permissions.includes("Allocation.Test"),
    allocationKeys: allocation,
  };

  const sim = await fetch(`${API}/allocation/simulate`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Authorization: `Bearer ${token}` },
    body: JSON.stringify({
      academicYearId: 1,
      courseId: 1,
      groupId: 2,
      semesterId: 3,
      groupingMode: "LastThreeDigits",
      enabledStrategies: { Capacity: true, Validation: true },
      populationSelection: { mode: "AllEligible" },
      targetSectionIds: null,
    }),
  });

  // SuperAdmin probe for engine path if Admin lacks Run
  const saLogin = await fetch(`${API}/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      universityCode: "001",
      collegeCode: "1053",
      username: "superadmin",
      password: "SuperAdmin@1",
    }),
  });
  const saBody = await saLogin.json();
  const saToken = saBody.token || saBody.accessToken;
  let saSummary = { loginOk: !!saToken };
  let saSim = { status: null };
  let saLast3 = null;
  if (saToken) {
    const saPerms = extractPermissions(saToken).permissions;
    saSummary = {
      loginOk: true,
      permissionCount: saPerms.length,
      hasAllocationRun: saPerms.includes("Allocation.Run"),
    };
    saSim = await fetch(`${API}/allocation/simulate`, {
      method: "POST",
      headers: { "Content-Type": "application/json", Authorization: `Bearer ${saToken}` },
      body: JSON.stringify({
        academicYearId: 1,
        courseId: 1,
        groupId: 2,
        semesterId: 3,
        groupingMode: "LastThreeDigits",
        enabledStrategies: { Capacity: true, Validation: true, Policy: true, Scoring: true },
        populationSelection: { mode: "AllEligible" },
        targetSectionIds: null,
      }),
    });
    const simBody = saSim.ok ? await saSim.json() : null;
    if (simBody) {
      const recs = simBody.recommendations || simBody.Recommendations || [];
      saLast3 = {
        http: saSim.status,
        recommendationCount: recs.length,
        scenarioIdPresent: !!(simBody.scenarioId || simBody.ScenarioId),
        first8: recs.slice(0, 8).map((r) => ({
          studentNumber: r.studentNumber || r.StudentNumber,
          last3: String(r.studentNumber || r.StudentNumber || "").slice(-3),
          toSection: r.toSectionCode || r.ToSectionCode || r.toSectionId || r.ToSectionId,
          fromSection: r.fromSectionCode || r.FromSectionCode,
        })),
      };
    } else {
      saLast3 = { http: saSim.status, body: (await saSim.text()).slice(0, 200) };
    }

    // Explicit one section + range using full student numbers from context
    const ctx = await (
      await fetch(`${API}/allocation/context?academicYearId=1&courseId=1&groupId=2&semesterId=3`, {
        headers: { Authorization: `Bearer ${saToken}` },
      })
    ).json();
    const students = ctx.students || ctx.Students || [];
    const sections = ctx.sections || ctx.Sections || [];
    const nums = students.map((s) => s.studentNumber || s.StudentNumber).filter(Boolean).sort();
    const sectionIds = sections.map((s) => s.sectionId || s.SectionId).filter(Boolean);

    // last-3 style range using actual last3 values that exist
    const last3s = nums.map((n) => n.slice(-3));
    const uniqueLast3 = [...new Set(last3s)].sort();
    const sampleLast3 = uniqueLast3.slice(0, 10);

    // Full-number range: first 5 full numbers ordinal
    const fromFull = nums[0];
    const toFull = nums[Math.min(4, nums.length - 1)];

    async function sim(label, payload) {
      const r = await fetch(`${API}/allocation/simulate`, {
        method: "POST",
        headers: { "Content-Type": "application/json", Authorization: `Bearer ${saToken}` },
        body: JSON.stringify(payload),
      });
      const b = r.ok ? await r.json() : { error: (await r.text()).slice(0, 300) };
      const recs = b.recommendations || b.Recommendations || [];
      return {
        label,
        http: r.status,
        recs: recs.length,
        scenarioIdPresent: !!(b.scenarioId || b.ScenarioId),
        sectionTargets: [...new Set(recs.map((x) => x.toSectionCode || x.ToSectionCode || x.toSectionId))],
        sample: recs.slice(0, 5).map((x) => ({
          n: x.studentNumber || x.StudentNumber,
          last3: String(x.studentNumber || x.StudentNumber || "").slice(-3),
          to: x.toSectionCode || x.ToSectionCode,
        })),
      };
    }

    const base = {
      academicYearId: 1,
      courseId: 1,
      groupId: 2,
      semesterId: 3,
      enabledStrategies: { Capacity: true, Validation: true },
    };

    const probes = {
      last3All: saLast3,
      range1to5: await sim("range-1-5", {
        ...base,
        groupingMode: "StudentNumber",
        populationSelection: { mode: "StudentNumberRange", fromStudentNumber: "1", toStudentNumber: "5" },
      }),
      range46to50: await sim("range-46-50", {
        ...base,
        groupingMode: "StudentNumber",
        populationSelection: { mode: "StudentNumberRange", fromStudentNumber: "46", toStudentNumber: "50" },
      }),
      rangeFullPrefix: await sim("range-full-first5", {
        ...base,
        groupingMode: "LastThreeDigits",
        populationSelection: {
          mode: "StudentNumberRange",
          fromStudentNumber: fromFull,
          toStudentNumber: toFull,
        },
      }),
      allEligible: await sim("all-eligible", {
        ...base,
        groupingMode: "LastThreeDigits",
        populationSelection: { mode: "AllEligible" },
        targetSectionIds: null,
      }),
      explicitOne:
        sectionIds.length > 0
          ? await sim("explicit-one", {
              ...base,
              groupingMode: "LastThreeDigits",
              populationSelection: { mode: "AllEligible" },
              targetSectionIds: [sectionIds[0]],
            })
          : { label: "explicit-one", http: 0, note: "no sections" },
    };

    // Prove Last3 order: extract ordered last3 from allEligible sample recommendations
    // Fetch more detail from allEligible via second call with Alphabetical vs Last3 compare counts
    const alpha = await sim("alpha-all", {
      ...base,
      groupingMode: "Alphabetical",
      populationSelection: { mode: "AllEligible" },
    });

    mkdirSync(OUT, { recursive: true });
    const out = {
      adminClaimsCorrected: summary,
      adminSimulateHttp: sim.status,
      superAdmin: saSummary,
      studentNumberNote: {
        format: "12-digit digits-only (college-prefixed)",
        samples: nums.slice(0, 8),
        uniqueLast3Sample: sampleLast3,
        fullRangeUsed: { from: fromFull, to: toFull },
      },
      existingSectioned: {
        withSection: students.filter((s) => (s.currentSectionId ?? s.CurrentSectionId) != null).length,
        without: students.filter((s) => (s.currentSectionId ?? s.CurrentSectionId) == null).length,
      },
      sections: sections.map((s) => ({
        id: s.sectionId || s.SectionId,
        code: s.sectionCode || s.SectionCode,
      })),
      probes,
      alphaVsLast3: { alphaRecs: alpha.recs, last3Recs: probes.allEligible.recs },
    };
    writeFileSync(join(OUT, "prompt2-engine-probes.json"), JSON.stringify(out, null, 2));
    console.log(
      JSON.stringify(
        {
          adminHasRun: summary.hasAllocationRun,
          adminAllocKeys: summary.allocationKeys,
          adminSim: sim.status,
          saHasRun: saSummary.hasAllocationRun,
          probes: Object.fromEntries(
            Object.entries(probes).map(([k, v]) => [k, { http: v.http, recs: v.recs ?? v.recommendationCount, scenario: v.scenarioIdPresent }]),
          ),
          existingSectioned: out.existingSectioned,
          sections: out.sections,
        },
        null,
        2,
      ),
    );
  } else {
    console.log(JSON.stringify({ admin: summary, adminSim: sim.status, superAdminLogin: false }, null, 2));
  }
}

main().catch((e) => {
  console.error(String(e?.stack || e));
  process.exit(1);
});
