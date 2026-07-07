import { Typography } from "@mui/material";
import { useEffect, useRef, useState } from "react";

export type AnimatedCountProps = {
  value: number;
  decimals?: number;
  suffix?: string;
  durationMs?: number;
  variant?: "h6" | "body1" | "body2";
};

export const AnimatedCount = ({
  value,
  decimals = 0,
  suffix = "",
  durationMs = 500,
  variant = "h6",
}: AnimatedCountProps) => {
  const [displayValue, setDisplayValue] = useState(value);
  const previousValue = useRef(value);
  const prefersReducedMotion =
    typeof window !== "undefined" &&
    window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  useEffect(() => {
    if (prefersReducedMotion || previousValue.current === value) {
      setDisplayValue(value);
      previousValue.current = value;
      return;
    }

    const start = previousValue.current;
    const delta = value - start;
    const startTime = performance.now();

    const frame = (now: number) => {
      const elapsed = now - startTime;
      const progress = Math.min(1, elapsed / durationMs);
      const eased = 1 - (1 - progress) ** 3;
      setDisplayValue(start + delta * eased);

      if (progress < 1) {
        requestAnimationFrame(frame);
      } else {
        previousValue.current = value;
        setDisplayValue(value);
      }
    };

    const id = requestAnimationFrame(frame);
    return () => cancelAnimationFrame(id);
  }, [durationMs, prefersReducedMotion, value]);

  const formatted =
    decimals > 0 ? displayValue.toFixed(decimals) : Math.round(displayValue).toString();

  return (
    <Typography variant={variant} component="p" sx={{ fontVariantNumeric: "tabular-nums" }}>
      {formatted}
      {suffix}
    </Typography>
  );
};

export default AnimatedCount;
