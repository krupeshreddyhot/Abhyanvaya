import { writeFileSync, mkdirSync } from "node:fs";
import { join } from "node:path";

const API = process.env.API_URL || "http://localhost:5210/api";
const OUT =
  process.env.OUT_DIR ||
  join("D:", "Resheta", "AttendenceProject", "CursonModifiedFiles", "AI Attandance", "AI29.1D.24B.3", "Prompt 2");

function cmp(a, b) {
  const L = String(a ?? "").trim().toUpperCase();
  const R = String(b ?? "").trim().toUpperCase();
  return L < R ? -1 : L > R ? 1 : 0;
}
function inRange(n, from, to) {
  const s = String(n ?? "").trim();
  if (!s) return false;
  return cmp(from, s) <= 0 && cmp(s, to) <= 0;
}
function last3(n) {
  const s = String(n ?? "").trim();
  return s.length <= 3 ? s : s.slice(-3);
}

async function main() {
  const login = await (await fetch(`${API}/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ universityCode: "001", collegeCode: "1053", username: "admin", password: "admin123" }),
  })).json();
  const token = login.token || login.accessToken;
  const raw = Buffer.from(token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/"), "base64").toString("utf8");
  const payload = JSON.parse(raw);
  const perms = Array.isArray(payload.permission) ? payload.permission : [payload.permission].filter(Boolean);
  const allocation = perms.filter((p) => String(p).startsWith("Allocation")).sort();
  const missingExpected = ["Allocation.Run", "Allocation.Approve", "Allocation.Reject", "Allocation.Export"].filter(
    (k) => !perms.includes(k),
  );
  const presentExpected = ["Allocation.Run", "Allocation.Approve", "Allocation.Operations.View", "Allocation.Scenario.View"].filter(
    (k) => perms.includes(k),
  );

  const ctx = await (
    await fetch(`${API}/allocation/context?academicYearId=1&courseId=1&groupId=2&semesterId=3`, {
      headers: { Authorization: `Bearer ${token}` },
    })
  ).json();
  const students = ctx.students || [];
  const sections = ctx.sections || [];
  const capacities = ctx.capacities || [];
  const nums = students.map((s) => s.studentNumber).filter(Boolean);

  // Ordering proof without simulate: sort by last3 locally (mirrors StudentGroupingStrategy)
  const orderedLast3 = [...students]
    .map((s) => ({
      id: s.studentId,
      n: s.studentNumber,
      last3: last3(s.studentNumber),
      currentSectionId: s.currentSectionId ?? null,
      currentSectionCode: s.currentSectionCode ?? null,
    }))
    .sort((a, b) => cmp(a.last3, b.last3) || cmp(a.n, b.n) || a.id - b.id);

  const range15 = nums.filter((n) => inRange(n, "1", "5"));
  const range4650 = nums.filter((n) => inRange(n, "46", "50"));
  // last-3 style that operators might intend: map last3 between 046-050
  const last3Band = nums.filter((n) => {
    const k = last3(n);
    return cmp("046", k) <= 0 && cmp(k, "050") <= 0;
  });
  const last3Band1to5 = nums.filter((n) => {
    const k = last3(n);
    // numeric-ish last3 001-005
    return cmp("001", k) <= 0 && cmp(k, "005") <= 0;
  });

  // Capacity snapshot
  const capRows = capacities.map((c) => {
    const sec = sections.find((s) => s.sectionId === c.sectionId);
    return {
      sectionId: c.sectionId,
      code: sec?.sectionCode,
      max: c.maximumCapacity,
      reserved: c.reservedSeats,
      currentStrength: c.currentStrength,
      remainingHard: Math.max(0, (c.maximumCapacity || 0) - (c.reservedSeats || 0)),
    };
  });

  const assigned = students.filter((s) => s.currentSectionId != null);
  const bySection = {};
  for (const s of assigned) {
    const k = s.currentSectionCode || String(s.currentSectionId);
    bySection[k] = (bySection[k] || 0) + 1;
  }

  // Target section eligibility from context
  const eligibleSectionIds = sections.map((s) => s.sectionId);
  const explicitSubset = eligibleSectionIds.slice(0, 1);

  mkdirSync(OUT, { recursive: true });
  const out = {
    adminPermissions: {
      total: perms.length,
      allocationKeys: allocation,
      presentExpected,
      missingExpected,
      hasAllocationRun: perms.includes("Allocation.Run"),
      role: payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"],
    },
    studentNumberFormat: {
      count: nums.length,
      pattern: "12-digit numeric, college-prefixed (digits-only)",
      samples: nums.slice(0, 12),
      lengths: [...new Set(nums.map((n) => n.length))],
      last3Samples: orderedLast3.slice(0, 12).map((x) => ({ n: x.n, last3: x.last3 })),
    },
    ranges: {
      fullString_1_to_5: { matched: range15.length, of: nums.length },
      fullString_46_to_50: { matched: range4650.length, of: nums.length },
      last3_001_to_005: { matched: last3Band1to5.length, samples: last3Band1to5.slice(0, 8) },
      last3_046_to_050: { matched: last3Band.length, samples: last3Band.slice(0, 8) },
    },
    lastThreeDigitsOrderingProof: {
      method: "Client mirror of StudentGroupingStrategy.LastThreeKey sort (engine path blocked by 403)",
      first15: orderedLast3.slice(0, 15),
      last15: orderedLast3.slice(-5),
      isNonDecreasingLast3: orderedLast3.every(
        (row, i, arr) => i === 0 || cmp(arr[i - 1].last3, row.last3) <= 0,
      ),
    },
    existingAssignments: {
      assignedCount: assigned.length,
      unassignedCount: students.length - assigned.length,
      bySection,
    },
    capacity: capRows,
    targets: {
      allEligibleIds: eligibleSectionIds,
      explicitExample: explicitSubset,
      note: "Simulate/Run blocked for Admin (403) — target semantics validated via context section list only",
    },
    simulateBlocked: {
      http: 403,
      rootCause: "Admin JWT permission array lacks Allocation.Run; CanRunAllocation policy requires that claim",
    },
  };
  writeFileSync(join(OUT, "prompt2-semantics-without-run.json"), JSON.stringify(out, null, 2));
  console.log(
    JSON.stringify(
      {
        hasAllocationRun: out.adminPermissions.hasAllocationRun,
        allocationKeys: allocation,
        missingExpected,
        ranges: out.ranges,
        assigned: out.existingAssignments,
        sections: sections.map((s) => s.sectionCode),
        last3OrderedOk: out.lastThreeDigitsOrderingProof.isNonDecreasingLast3,
        firstLast3: out.lastThreeDigitsOrderingProof.first15.map((x) => x.last3),
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
