import { Navigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";

type ProtectedRouteProps = {
  children: React.ReactNode;
  /** Legacy: require one of these roles (case-insensitive). */
  allowedRoles?: string[];
  /** Require at least one of these JWT `permission` claim values. */
  anyPermission?: string[];
  /**
   * When true with both `allowedRoles` and `anyPermission`, pass if either matches.
   * Use for Catalog hub: tenant Admin **or** any setup-related permission.
   */
  allowRoleOrPermission?: boolean;
};

const ProtectedRoute = ({
  children,
  allowedRoles,
  anyPermission,
  allowRoleOrPermission,
}: ProtectedRouteProps) => {
  const { isAuthenticated, user, hasAnyPermission } = useAuth();

  if (!isAuthenticated) {
    return <Navigate to="/" replace />;
  }

  const currentRole = (user?.role ?? "").toLowerCase();
  const roleOk =
    !!allowedRoles?.length &&
    allowedRoles.some((role) => role.toLowerCase() === currentRole);
  const permOk =
    !!anyPermission?.length && hasAnyPermission(anyPermission ?? []);

  if (allowRoleOrPermission && (allowedRoles?.length || anyPermission?.length)) {
    const passes = (allowedRoles?.length ? roleOk : false) || (anyPermission?.length ? permOk : false);
    if (!passes) {
      return <Navigate to="/dashboard" replace />;
    }
    return <>{children}</>;
  }

  if (anyPermission && anyPermission.length > 0) {
    if (!hasAnyPermission(anyPermission)) {
      return <Navigate to="/dashboard" replace />;
    }
    return <>{children}</>;
  }

  if (allowedRoles && allowedRoles.length > 0) {
    const isAllowed = allowedRoles.some((role) => role.toLowerCase() === currentRole);

    if (!isAllowed) {
      return <Navigate to="/dashboard" replace />;
    }
  }

  return <>{children}</>;
};

export default ProtectedRoute;
