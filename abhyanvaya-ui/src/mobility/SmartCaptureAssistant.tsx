import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import { Alert, Stack, Typography } from "@mui/material";
import { getImageQualityIndicator } from "../utils/imageQuality";

export type CaptureAssistantHints = {
  lighting?: "ok" | "dark" | "bright";
  blurScore?: number | null;
  estimatedFaces?: number | null;
  stability?: "ok" | "shaking";
  distance?: "ok" | "too-close" | "too-far";
  framing?: "ok" | "left" | "right" | "up" | "down";
};

export type SmartCaptureAssistantProps = {
  hints: CaptureAssistantHints;
  /** Guidance never blocks capture — informational only. */
  dense?: boolean;
};

/**
 * AI22.7C Phase 1.5 — Smart Capture Assistant (guidance only, never blocks).
 * Reuses image quality / blur pipeline signals when available.
 */
export function buildCaptureGuidance(hints: CaptureAssistantHints): string[] {
  const tips: string[] = [];
  const quality = getImageQualityIndicator(hints.blurScore ?? null);

  if (hints.lighting === "dark") {
    tips.push("Too dark — face a light source or open blinds.");
  } else if (hints.lighting === "bright") {
    tips.push("Too bright — avoid harsh backlight.");
  }

  if (quality.rank > 0 && quality.rank < 3) {
    tips.push("Too blurry — hold steady and retake.");
  }

  if (hints.stability === "shaking") {
    tips.push("Camera shaking — brace your elbows or use both hands.");
  }

  if (hints.distance === "too-close") {
    tips.push("Move back so more students fit in frame.");
  } else if (hints.distance === "too-far") {
    tips.push("Move closer — faces look small.");
  }

  if (hints.framing === "left") {
    tips.push("Move slightly left.");
  } else if (hints.framing === "right") {
    tips.push("Move slightly right.");
  } else if (hints.framing === "up") {
    tips.push("Tilt up slightly.");
  } else if (hints.framing === "down") {
    tips.push("Tilt down slightly.");
  }

  if (hints.estimatedFaces != null && hints.estimatedFaces === 0) {
    tips.push("No faces estimated yet — check framing.");
  }

  return tips;
}

export function SmartCaptureAssistant({ hints, dense = false }: SmartCaptureAssistantProps) {
  const tips = buildCaptureGuidance(hints);
  if (tips.length === 0) {
    return (
      <Alert severity="success" icon={<WarningAmberIcon fontSize="inherit" />} sx={{ py: dense ? 0.5 : 1 }}>
        Framing looks ready — capture when the class is settled.
      </Alert>
    );
  }

  return (
    <Stack spacing={0.75} aria-live="polite" aria-label="Capture guidance">
      <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700 }}>
        Smart Capture Assistant (guidance only — never blocks capture)
      </Typography>
      {tips.map((tip) => (
        <Alert key={tip} severity="warning" sx={{ py: dense ? 0.25 : 0.75 }}>
          {tip}
        </Alert>
      ))}
    </Stack>
  );
}

export default SmartCaptureAssistant;
