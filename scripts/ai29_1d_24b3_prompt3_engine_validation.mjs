/**
 * AI29.1D.24B.3 Prompt 3 — live engine validation after Allocation.Run repair.
 * No tokens/passwords printed.
 */
import { writeFileSync, mkdirSync } from "node:fs";
import { join } from "node:path";

const API = process.env.API_URL || "http://localhost:5210/api";
const OUT =
  process.env.OUT_DIR ||
  join("D:", "Resheta", "AttendenceProject", "CursonModifiedFiles", "AI Attandance", "AI29.1D.24B.3", "Prompt 3");

async function api(token, path, opts = {}) {
  const res = await fetch(`${API}${path}`, {
    ...opts,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(opts.headers || {}),
    },
  });
  const text = await res.text();
  let body = null;
  try {
    body = text ? JSON.parse(text) : null;
  } catch {
    body = text?.slice?.(0, 400) ?? text;
  }
  return { ok: res.ok, status: res.status, body };
}

function extractPermissions(token) {
  const raw = Buffer.from(token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/"), "base64").toString("utf8");
  const payload = JSON.parse(raw);
  return Array.isArray(payload.permission) ? payload.permission : [];
}

function last3(n) {
  const s = String(n ?? "").trim();
  return s.length <= 3 ? s : s.slice(-3);
}

function cmp(a, b) {
  const L = String(a ?? "").trim().toUpperCase();
  const R = String(b ?? "").trim().toUpperCase();
  return L < R ? -1 : L > R ? 1 : 0;
}

async function main() {
  mkdirSync(OUT, { recursive: true });
  const login = await api(null, "/auth/login", {
    method: "POST",
    body: JSON.stringify({
      universityCode: "001",
      collegeCode: "1053",
      username: "admin",
      password: "admin123",
    }),
  });
  const token = login.body?.token || login.body?.accessToken;
  const perms = extractPermissions(token);
  const ctx = await api(token, "/allocation/context?academicYearId=1&courseId=1&groupId=2&semesterId=3");
  const students = ctx.body?.students || [];
  const sections = ctx.body?.sections || [];
  const capacities = ctx.body?.capacities || [];
  const sectionIds = sections.map((s) => s.sectionId).filter(Boolean);
  const nums = students.map((s) => s.studentNumber).filter(Boolean).sort(cmp);

  const base = {
    academicYearId: 1,
    courseId: 1,
    groupId: 2,
    semesterId: 3,
    enabledStrategies: { Validation: true, Capacity: true, Policy: true, Scoring: true },
  };

  async function sim(label, payload) {
    const r = await api(token, "/allocation/simulate", { method: "POST", body: JSON.stringify(payload) });
    const scenario = r.body?.scenario || {};
    const recs = scenario.recommendations || r.body?.recommendations || r.body?.Recommendations || [];
    const warnings = r.body?.warnings || r.body?.Warnings || [];
    const errors = r.body?.errors || r.body?.Errors || [];
    const summaries = scenario.sectionSummaries || [];
    return {
      label,
      http: r.status,
      ok: r.ok,
      succeeded: r.body?.succeeded === true,
      recs: recs.length,
      scenarioIdPresent: !!(r.body?.scenarioId || scenario.scenarioId),
      warnings: Array.isArray(warnings) ? warnings.length : 0,
      errors: Array.isArray(errors) ? errors.slice(0, 5) : [errors],
      summaries: summaries.map((s) => ({
        code: s.sectionCode,
        assigned: s.assignedCount,
        max: s.maximumCapacity,
        occ: s.occupancyPercent,
      })),
      sample: recs.slice(0, 8).map((x) => ({
        n: x.studentNumber || x.StudentNumber,
        last3: last3(x.studentNumber || x.StudentNumber),
        from: x.fromSectionCode || x.FromSectionCode,
        to: x.toSectionCode || x.ToSectionCode,
        kept: Array.isArray(x.explanations) && x.explanations.some((e) => /Kept in section/i.test(String(e))),
      })),
      toSections: [...new Set(recs.map((x) => x.toSectionCode || x.ToSectionCode).filter(Boolean))],
    };
  }

  const fromFull = nums[0];
  const toFull = nums[Math.min(9, nums.length - 1)];

  const probes = {
    preview_last3_allEligible: await sim("preview-last3-all", {
      ...base,
      groupingMode: "LastThreeDigits",
      populationSelection: { mode: "AllEligible" },
      targetSectionIds: null,
    }),
    simulation_last3_allEligible: await sim("sim-last3-all", {
      ...base,
      groupingMode: "LastThreeDigits",
      populationSelection: { mode: "AllEligible" },
      targetSectionIds: null,
    }),
    explicit_one_section: sectionIds.length
      ? await sim("explicit-one", {
          ...base,
          groupingMode: "LastThreeDigits",
          populationSelection: { mode: "AllEligible" },
          targetSectionIds: [sectionIds[0]],
        })
      : { label: "explicit-one", ok: false },
    filtered_capacity: fromFull
      ? await sim("filtered-cap", {
          ...base,
          groupingMode: "LastThreeDigits",
          populationSelection: {
            mode: "StudentNumberRange",
            fromStudentNumber: fromFull,
            toStudentNumber: toFull,
          },
          targetSectionIds: null,
        })
      : { ok: false },
  };

  const run = await api(token, "/allocation/run", {
    method: "POST",
    body: JSON.stringify({
      ...base,
      groupingMode: "LastThreeDigits",
      populationSelection: { mode: "AllEligible" },
      targetSectionIds: null,
    }),
  });
  const runRecs =
    run.body?.scenario?.recommendations || run.body?.recommendations || run.body?.Recommendations || [];

  // Already assigned observation from context + simulate sample
  const assigned = students.filter((s) => s.currentSectionId != null);

  // Governance: try review endpoint if scenario present
  const scenarioId = probes.preview_last3_allEligible.scenarioIdPresent
    ? (
        await api(token, "/allocation/simulate", {
          method: "POST",
          body: JSON.stringify({
            ...base,
            groupingMode: "Alphabetical",
            populationSelection: { mode: "AllEligible" },
          }),
        })
      ).body?.scenarioId || null
    : null;

  let governance = { status: "NOT EXECUTED" };
  if (scenarioId) {
    const detail = await api(token, `/allocation/scenarios/${scenarioId}`);
    const review = await api(token, `/allocation/scenarios/${scenarioId}/review`, { method: "POST", body: "{}" });
    governance = {
      detailHttp: detail.status,
      reviewHttp: review.status,
      canApprove: detail.body?.governance?.canApprove ?? detail.body?.canApprove ?? null,
      status: review.ok || detail.ok ? "EXECUTED" : "FAIL",
    };
  }

  // Faculty denied already proven in repair; confirm again
  const fac = await api(null, "/auth/login", {
    method: "POST",
    body: JSON.stringify({
      universityCode: "001",
      collegeCode: "1053",
      username: "teststaff1",
      password: "teststaff1f",
    }),
  });
  const facToken = fac.body?.token || fac.body?.accessToken;
  const facSim = facToken
    ? await api(facToken, "/allocation/simulate", {
        method: "POST",
        body: JSON.stringify({
          ...base,
          groupingMode: "Alphabetical",
          populationSelection: { mode: "AllEligible" },
        }),
      })
    : { status: fac.status };

  const last3OrderOk = (() => {
    const sample = probes.preview_last3_allEligible.sample || [];
    if (sample.length < 2) return null;
    for (let i = 1; i < sample.length; i++) {
      if (cmp(sample[i - 1].last3, sample[i].last3) > 0) return false;
    }
    return true;
  })();

  const report = {
    date: new Date().toISOString(),
    auth: {
      hasAllocationRun: perms.includes("Allocation.Run"),
      allocationKeyCount: perms.filter((p) => String(p).startsWith("Allocation")).length,
    },
    context: {
      students: students.length,
      sections: sections.map((s) => s.sectionCode),
      capacities: capacities.length,
      alreadyAssigned: assigned.length,
    },
    probes,
    run: {
      http: run.status,
      ok: run.ok,
      recs: runRecs.length,
      scenarioIdPresent: !!(run.body?.scenarioId || run.body?.ScenarioId),
    },
    last3OrderOk,
    governance,
    facultyDenied: facSim.status === 403,
  };

  writeFileSync(join(OUT, "prompt3-engine-validation.json"), JSON.stringify(report, null, 2));
  console.log(
    JSON.stringify(
      {
        hasRun: report.auth.hasAllocationRun,
        preview: { http: probes.preview_last3_allEligible.http, recs: probes.preview_last3_allEligible.recs, scenario: probes.preview_last3_allEligible.scenarioIdPresent, to: probes.preview_last3_allEligible.toSections },
        simulation: { http: probes.simulation_last3_allEligible.http, recs: probes.simulation_last3_allEligible.recs },
        explicit: { http: probes.explicit_one_section.http, recs: probes.explicit_one_section.recs, to: probes.explicit_one_section.toSections },
        filtered: { http: probes.filtered_capacity.http, recs: probes.filtered_capacity.recs },
        run: report.run,
        last3OrderOk,
        alreadyAssigned: assigned.length,
        facultyDenied: report.facultyDenied,
        governance,
        previewSummaries: probes.preview_last3_allEligible.summaries,
        previewWarnings: probes.preview_last3_allEligible.warnings,
        keptSample: (probes.preview_last3_allEligible.sample || []).filter((x) => x.kept).length,
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
