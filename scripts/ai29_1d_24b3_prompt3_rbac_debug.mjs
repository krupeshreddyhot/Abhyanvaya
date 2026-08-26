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
    body = text?.slice?.(0, 500) ?? text;
  }
  return { ok: res.ok, status: res.status, body, text: text?.slice?.(0, 300) };
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
  const roles = await api(token, "/tenant-rbac/roles");
  const perms = await api(token, "/tenant-rbac/permissions");
  const roleList = Array.isArray(roles.body) ? roles.body : [];
  const admin = roleList.find((r) => String(r.code || "").toUpperCase() === "ADMIN");
  const detail = admin ? await api(token, `/tenant-rbac/roles/${admin.id}`) : null;
  const catalog = Array.isArray(perms.body) ? perms.body : [];
  const alloc = catalog.filter((p) => String(p.key || "").startsWith("Allocation"));

  const out = {
    rolesStatus: roles.status,
    roleCount: roleList.length,
    roleCodes: roleList.map((r) => ({ id: r.id, code: r.code, permCount: r.permissionCount })),
    adminDetailStatus: detail?.status,
    adminPermissionIdCount: (detail?.body?.permissionIds || []).length,
    adminHas227: (detail?.body?.permissionIds || []).includes(227),
    allocationCatalog: alloc.map((p) => ({ id: p.id, key: p.key })),
  };
  writeFileSync(join(OUT, "prompt3-rbac-debug.json"), JSON.stringify(out, null, 2));
  console.log(JSON.stringify(out, null, 2));
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
