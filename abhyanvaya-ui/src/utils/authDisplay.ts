const parseJwtPayload = (token: string): Record<string, unknown> | null => {
  try {
    const payload = token.split(".")[1];
    if (!payload) return null;

    const normalized = payload.replace(/-/g, "+").replace(/_/g, "/");
    const decoded = atob(normalized.padEnd(Math.ceil(normalized.length / 4) * 4, "="));

    return JSON.parse(decoded) as Record<string, unknown>;
  } catch {
    return null;
  }
};

const readNameClaim = (claims: Record<string, unknown>): string | null => {
  const candidates = [
    claims.unique_name,
    claims.name,
    claims.Name,
    claims.UserName,
    claims.username,
    claims["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"],
  ];

  for (const candidate of candidates) {
    if (typeof candidate === "string" && candidate.trim().length > 0) {
      return candidate.trim();
    }
  }

  return null;
};

export type AuthenticatedFacultyInfo = {
  name: string;
  title: string;
  role: string;
  department: string;
  todaysClasses: number;
};

/** Maps JWT role to a display title (placeholder until staff profile API). */
const resolveFacultyTitle = (role?: string | null): string => {
  const normalized = role?.trim().toLowerCase() ?? "";
  if (normalized === "admin") {
    return "Assistant Professor";
  }

  if (normalized === "faculty" || normalized === "teacher") {
    return "Assistant Professor";
  }

  return role?.trim() || "Faculty";
};

/** Display label for the authenticated faculty user without additional API calls. */
export const getAuthenticatedFacultyLabel = (
  token: string | null,
  role?: string | null,
): string => getAuthenticatedFacultyInfo(token, role).name;

/** Faculty dashboard details derived from JWT claims (no extra API call). */
export const getAuthenticatedFacultyInfo = (
  token: string | null,
  role?: string | null,
): AuthenticatedFacultyInfo => {
  let name: string | null = null;

  if (token) {
    const claims = parseJwtPayload(token);
    name = claims ? readNameClaim(claims) : null;
  }

  const resolvedRole = role?.trim() || "Faculty";

  return {
    name: name ?? "Current Faculty",
    title: resolveFacultyTitle(role),
    role: resolvedRole,
    department: "Computer Applications",
    todaysClasses: 5,
  };
};
