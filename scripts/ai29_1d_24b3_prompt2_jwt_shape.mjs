import { writeFileSync, mkdirSync } from "node:fs";
import { join } from "node:path";

const API = process.env.API_URL || "http://localhost:5210/api";
const OUT =
  process.env.OUT_DIR ||
  join("D:", "Resheta", "AttendenceProject", "CursonModifiedFiles", "AI Attandance", "AI29.1D.24B.3", "Prompt 2");

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
  const raw = Buffer.from(token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/"), "base64").toString("utf8");
  const payload = JSON.parse(raw);

  // Claim type inventory (values redacted except permission-like)
  const claimTypes = Object.keys(payload).sort();
  const permissionLike = [];
  for (const [k, v] of Object.entries(payload)) {
    if (/permission|Permission|role|Role/i.test(k)) {
      if (Array.isArray(v)) permissionLike.push({ key: k, count: v.length, sample: v.slice(0, 5), hasRun: v.includes("Allocation.Run") });
      else permissionLike.push({ key: k, valueType: typeof v, valuePreview: String(v).slice(0, 80), isRun: v === "Allocation.Run" });
    }
  }

  // Search raw for Allocation.Run string presence
  const hasRunInRaw = raw.includes("Allocation.Run");
  const allocMentions = [...raw.matchAll(/Allocation\.[A-Za-z.]+/g)].map((m) => m[0]);
  const uniqueAlloc = [...new Set(allocMentions)].sort();

  // How many times does "permission" appear as substring
  const permissionWordCount = (raw.match(/permission/gi) || []).length;

  mkdirSync(OUT, { recursive: true });
  const out = {
    claimTypes,
    permissionLike,
    hasRunInRaw,
    uniqueAllocInRaw: uniqueAlloc,
    permissionWordCount,
    payloadValueShapes: Object.fromEntries(
      Object.entries(payload).map(([k, v]) => [
        k,
        Array.isArray(v) ? `array(${v.length})` : `${typeof v}:${String(v).slice(0, 40)}`,
      ]),
    ),
  };
  writeFileSync(join(OUT, "prompt2-jwt-claim-shape.json"), JSON.stringify(out, null, 2));
  console.log(JSON.stringify(out, null, 2));
}

main().catch((e) => {
  console.error(String(e?.stack || e));
  process.exit(1);
});
