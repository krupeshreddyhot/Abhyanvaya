import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import {
  clearContext as clearContextApi,
  getCurrentContext,
  searchAvailableColleges,
  setCollegeContext,
  type AvailableCollegeDto,
  type TenantContextSnapshot,
} from "../api/tenantContextApiClient";
import { useAuth } from "./AuthContext";
import { getApiErrorMessage } from "../utils/apiErrorMessage";

type TenantContextValue = {
  context: TenantContextSnapshot | null;
  loading: boolean;
  error: string | null;
  isSuperAdmin: boolean;
  needsCollegeSelection: boolean;
  refresh: () => Promise<void>;
  selectCollege: (collegeId: number) => Promise<boolean>;
  clearOperationalContext: () => Promise<void>;
  searchColleges: (search: string) => Promise<AvailableCollegeDto[]>;
};

const TenantContextState = createContext<TenantContextValue | null>(null);

export const TenantContextProvider = ({ children }: { children: ReactNode }) => {
  const { token, user } = useAuth();
  const [context, setContext] = useState<TenantContextSnapshot | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const isSuperAdmin = user?.role === "SuperAdmin";

  const refresh = useCallback(async () => {
    if (!token) {
      setContext(null);
      setLoading(false);
      return;
    }

    setLoading(true);
    try {
      const res = await getCurrentContext();
      setContext(res.data);
      setError(null);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, [token]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const selectCollege = useCallback(async (collegeId: number) => {
    try {
      const res = await setCollegeContext({ collegeId });
      setContext(res.data);
      setError(null);
      return true;
    } catch (err) {
      setError(getApiErrorMessage(err));
      return false;
    }
  }, []);

  const clearOperationalContext = useCallback(async () => {
    try {
      await clearContextApi();
      await refresh();
    } catch (err) {
      setError(getApiErrorMessage(err));
    }
  }, [refresh]);

  const searchColleges = useCallback(async (search: string) => {
    const res = await searchAvailableColleges({ search, page: 1, pageSize: 25 });
    return res.data.items;
  }, []);

  const needsCollegeSelection = isSuperAdmin && (context?.isGlobal === true || !context?.selectedCollegeId);

  const value = useMemo<TenantContextValue>(
    () => ({
      context,
      loading,
      error,
      isSuperAdmin,
      needsCollegeSelection,
      refresh,
      selectCollege,
      clearOperationalContext,
      searchColleges,
    }),
    [
      context,
      loading,
      error,
      isSuperAdmin,
      needsCollegeSelection,
      refresh,
      selectCollege,
      clearOperationalContext,
      searchColleges,
    ],
  );

  return <TenantContextState.Provider value={value}>{children}</TenantContextState.Provider>;
};

export const useTenantContext = (): TenantContextValue => {
  const ctx = useContext(TenantContextState);
  if (!ctx) {
    throw new Error("useTenantContext must be used within TenantContextProvider");
  }
  return ctx;
};
