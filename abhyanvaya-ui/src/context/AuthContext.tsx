import { createContext, useContext, useMemo, useState } from "react";

type UserClaims = {
  userId: number;
  role: string;
  tenantId: number;
  courseId: number;
  groupId: number;
};

interface AuthContextType {
  token: string | null;
  user: UserClaims | null;
  login: (token: string) => void;
  logout: () => void;
  isAuthenticated: boolean;
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

const getUserClaims = (token: string | null): UserClaims | null => {
  if (!token) return null;

  const claims = parseJwt(token);
  if (!claims) return null;

  const toNum = (value: unknown) => Number.parseInt(String(value ?? "0"), 10) || 0;

  return {
    userId: toNum(claims.UserId),
    role: String(claims["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] ?? claims.role ?? claims.Role ?? ""),
    tenantId: toNum(claims.TenantId),
    courseId: toNum(claims.CourseId),
    groupId: toNum(claims.GroupId),
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

  return (
    <AuthContext.Provider
      value={{
        token,
        user,
        login: handleLogin,
        logout: handleLogout,
        isAuthenticated: !!token,
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

