import { createContext, useCallback, useContext, useMemo, useState } from "react";

type UserClaims = {
  userId: number;
  role: string;
  tenantId: number;
  courseId: number;
  groupId: number;
  /** Directory row when Faculty login is linked to staff (JWT <c>StaffId</c>). */
  staffId: number;
  permissions: string[];
  /** From JWT <c>must_change_password</c> claim. */
  mustChangePassword: boolean;
};

interface AuthContextType {
  token: string | null;
  user: UserClaims | null;
  login: (token: string) => void;
  logout: () => void;
  isAuthenticated: boolean;
  hasPermission: (key: string) => boolean;
  hasAnyPermission: (keys: string[]) => boolean;
}

const AuthContext = createContext<AuthContextType | null>(null);

const parseJwt = (token: string): Record<string, unknown> | null => {
  try {
    const payload = token.split(".")[1];
    if (!payload) return null;

    const normalized = payload.replace(/-/g, "+").replace(/_/g, "/");
    const decoded = atob(normalized.padEnd(Math.ceil(normalized.length / 4) * 4, "="));

    return JSON.parse(decoded);
  } catch {
    return null;
  }
};

/** Reads duplicate JWT claim type `permission` (string or string[]). */
const extractPermissions = (claims: Record<string, unknown>): string[] => {
  const raw = claims.permission ?? claims.Permission;
  if (Array.isArray(raw)) return raw.map((x) => String(x));
  if (typeof raw === "string" && raw.length > 0) return [raw];
  return [];
};

const getUserClaims = (token: string | null): UserClaims | null => {
  if (!token) return null;

  const claims = parseJwt(token);
  if (!claims) return null;

  const toNum = (value: unknown) => Number.parseInt(String(value ?? "0"), 10) || 0;

  const mc =
    claims.must_change_password === true ||
    claims.must_change_password === "true" ||
    claims.MustChangePassword === true ||
    claims.MustChangePassword === "true";

  return {
    userId: toNum(claims.UserId),
    role: String(claims["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] ?? claims.role ?? claims.Role ?? ""),
    tenantId: toNum(claims.TenantId),
    courseId: toNum(claims.CourseId),
    groupId: toNum(claims.GroupId),
    staffId: toNum(claims.StaffId),
    permissions: extractPermissions(claims),
    mustChangePassword: Boolean(mc),
  };
};

export const AuthProvider = ({ children }: { children: React.ReactNode }) => {
  const [token, setToken] = useState<string | null>(localStorage.getItem("token"));

  const handleLogin = (nextToken: string) => {
    localStorage.setItem("token", nextToken);
    setToken(nextToken);
  };

  const handleLogout = () => {
    localStorage.removeItem("token");
    setToken(null);
  };

  const user = useMemo(() => getUserClaims(token), [token]);

  const hasPermission = useCallback(
    (key: string) => (user?.permissions ?? []).includes(key),
    [user?.permissions],
  );

  const hasAnyPermission = useCallback(
    (keys: string[]) => keys.some((k) => hasPermission(k)),
    [hasPermission],
  );

  return (
    <AuthContext.Provider
      value={{
        token,
        user,
        login: handleLogin,
        logout: handleLogout,
        isAuthenticated: !!token,
        hasPermission,
        hasAnyPermission,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) throw new Error("useAuth must be used within AuthProvider");
  return context;
};

