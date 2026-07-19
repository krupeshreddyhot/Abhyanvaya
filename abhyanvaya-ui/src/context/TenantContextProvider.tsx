import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import {
  clearContext as clearContextApi,
  getCurrentContext,
  getRecentColleges,
  refreshContext,
  searchAvailableColleges,
  setCollegeContext,
  type AvailableCollegeDto,
  type RecentCollegeEntry,
  type TenantContextSnapshot,
} from "../api/tenantContextApiClient";
import { useAuth } from "./AuthContext";
import { getApiErrorMessage } from "../utils/apiErrorMessage";

export type ContextEventType = "ContextChanged" | "ContextCleared" | "ContextExpired" | "ContextRestored";

type TenantContextValue = {
  context: TenantContextSnapshot | null;
  loading: boolean;
  error: string | null;
  isSuperAdmin: boolean;
  hasOperationalContext: boolean;
  refresh: () => Promise<void>;
  selectCollege: (collegeId: number) => Promise<boolean>;
  clearOperationalContext: () => Promise<void>;
  renewOperationalContext: () => Promise<boolean>;
  searchColleges: (search: string) => Promise<AvailableCollegeDto[]>;
  getRecentColleges: () => Promise<{ recent: RecentCollegeEntry[]; popular: AvailableCollegeDto[] }>;
  subscribe: (event: ContextEventType, handler: () => void) => () => void;
};

const TenantContextState = createContext<TenantContextValue | null>(null);

export const TenantContextProvider = ({ children }: { children: ReactNode }) => {
  const { token, user } = useAuth();
  const [context, setContext] = useState<TenantContextSnapshot | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const listenersRef = useRef<Map<ContextEventType, Set<() => void>>>(new Map());

  const isSuperAdmin = user?.role === "SuperAdmin";

  const publish = useCallback((event: ContextEventType) => {
    listenersRef.current.get(event)?.forEach((handler) => handler());
  }, []);

  const subscribe = useCallback((event: ContextEventType, handler: () => void) => {
    const map = listenersRef.current;
    if (!map.has(event)) {
      map.set(event, new Set());
    }
    map.get(event)!.add(handler);
    return () => {
      map.get(event)?.delete(handler);
    };
  }, []);

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

  const selectCollege = useCallback(
    async (collegeId: number) => {
      try {
        const res = await setCollegeContext({ collegeId });
        setContext(res.data);
        setError(null);
        publish("ContextChanged");
        return true;
      } catch (err) {
        setError(getApiErrorMessage(err));
        return false;
      }
    },
    [publish],
  );

  const clearOperationalContext = useCallback(async () => {
    try {
      await clearContextApi();
      await refresh();
      publish("ContextCleared");
    } catch (err) {
      setError(getApiErrorMessage(err));
    }
  }, [publish, refresh]);

  const renewOperationalContext = useCallback(async () => {
    try {
      await refreshContext();
      await refresh();
      publish("ContextRestored");
      return true;
    } catch {
      return false;
    }
  }, [publish, refresh]);

  const searchColleges = useCallback(async (search: string) => {
    const res = await searchAvailableColleges({ search, page: 1, pageSize: 25 });
    return res.data.items;
  }, []);

  const loadRecentColleges = useCallback(async () => {
    const res = await getRecentColleges();
    return res.data;
  }, []);

  const hasOperationalContext =
    !isSuperAdmin || (context?.isGlobal === false && (context?.selectedCollegeId ?? 0) > 0);

  const value = useMemo<TenantContextValue>(
    () => ({
      context,
      loading,
      error,
      isSuperAdmin,
      hasOperationalContext,
      refresh,
      selectCollege,
      clearOperationalContext,
      renewOperationalContext,
      searchColleges,
      getRecentColleges: loadRecentColleges,
      subscribe,
    }),
    [
      context,
      loading,
      error,
      isSuperAdmin,
      hasOperationalContext,
      refresh,
      selectCollege,
      clearOperationalContext,
      renewOperationalContext,
      searchColleges,
      loadRecentColleges,
      subscribe,
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

/** @deprecated Use hasOperationalContext from useTenantContext instead. */
export const useNeedsCollegeSelection = (): boolean => {
  const { isSuperAdmin, hasOperationalContext } = useTenantContext();
  return isSuperAdmin && !hasOperationalContext;
};
