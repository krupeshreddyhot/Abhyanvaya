import { useEffect, useMemo, useState } from "react";
import { useTheme } from "@mui/material/styles";
import { enterpriseMotion } from "./enterpriseTokens";
import { useThemeManagerOptional } from "./ThemeManager";

/**
 * AI22.7B Phase 5.6 — enterprise motion helpers (Material Motion + reduced motion).
 */
export function usePrefersReducedMotion(): boolean {
  const manager = useThemeManagerOptional();
  const override = manager?.prefs.reducedMotionOverride ?? "system";
  const [systemReduce, setSystemReduce] = useState(() =>
    typeof window !== "undefined"
      ? window.matchMedia("(prefers-reduced-motion: reduce)").matches
      : false,
  );

  useEffect(() => {
    if (typeof window === "undefined") {
      return;
    }
    const mq = window.matchMedia("(prefers-reduced-motion: reduce)");
    const onChange = () => setSystemReduce(mq.matches);
    mq.addEventListener("change", onChange);
    return () => mq.removeEventListener("change", onChange);
  }, []);

  if (override === "reduce") {
    return true;
  }
  if (override === "no-preference") {
    return false;
  }
  return systemReduce;
}

export function useEnterpriseMotion() {
  const theme = useTheme();
  const reduce = usePrefersReducedMotion();

  return useMemo(() => {
    const duration = (ms: number) => (reduce ? 0 : ms);
    return {
      reduce,
      duration,
      shortest: duration(theme.transitions.duration.shortest),
      shorter: duration(theme.transitions.duration.shorter),
      short: duration(theme.transitions.duration.short),
      standard: duration(theme.transitions.duration.standard),
      easing: enterpriseMotion.easing.easeInOut,
      create: (props: string | string[]) =>
        reduce
          ? "none"
          : theme.transitions.create(props, {
              duration: theme.transitions.duration.shorter,
              easing: theme.transitions.easing.easeInOut,
            }),
      highlightPulse: reduce
        ? undefined
        : "enterprise-highlight-pulse 600ms ease-out 1",
    };
  }, [reduce, theme]);
}
