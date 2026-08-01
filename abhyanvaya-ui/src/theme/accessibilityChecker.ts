/**
 * AI22.7B Phase 5.3 — lightweight accessibility checker (WCAG 2.2 AA oriented).
 * Runs in-browser heuristics; does not replace axe/CI tooling.
 */

export type AccessibilityFinding = {
  id: string;
  severity: "error" | "warning" | "info";
  rule: string;
  message: string;
  selector?: string;
};

export type AccessibilityReport = {
  generatedAt: string;
  score: number;
  passed: number;
  warnings: number;
  errors: number;
  findings: AccessibilityFinding[];
};

function contrastRatio(fg: string, bg: string): number | null {
  const a = parseRgb(fg);
  const b = parseRgb(bg);
  if (!a || !b) {
    return null;
  }
  const l1 = relativeLuminance(a);
  const l2 = relativeLuminance(b);
  const lighter = Math.max(l1, l2);
  const darker = Math.min(l1, l2);
  return (lighter + 0.05) / (darker + 0.05);
}

function parseRgb(input: string): [number, number, number] | null {
  const hex = input.trim();
  if (hex.startsWith("#") && (hex.length === 7 || hex.length === 4)) {
    if (hex.length === 7) {
      return [
        Number.parseInt(hex.slice(1, 3), 16),
        Number.parseInt(hex.slice(3, 5), 16),
        Number.parseInt(hex.slice(5, 7), 16),
      ];
    }
    return [
      Number.parseInt(hex[1] + hex[1], 16),
      Number.parseInt(hex[2] + hex[2], 16),
      Number.parseInt(hex[3] + hex[3], 16),
    ];
  }
  const m = hex.match(/rgba?\((\d+),\s*(\d+),\s*(\d+)/i);
  if (!m) {
    return null;
  }
  return [Number(m[1]), Number(m[2]), Number(m[3])];
}

function relativeLuminance([r, g, b]: [number, number, number]): number {
  const toLin = (c: number) => {
    const s = c / 255;
    return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
  };
  return 0.2126 * toLin(r) + 0.7152 * toLin(g) + 0.0722 * toLin(b);
}

/** Evaluate a DOM subtree for common WCAG 2.2 AA issues. */
export function runAccessibilityChecker(root: ParentNode = document): AccessibilityReport {
  const findings: AccessibilityFinding[] = [];

  const images = root.querySelectorAll("img");
  images.forEach((img, index) => {
    if (!img.hasAttribute("alt")) {
      findings.push({
        id: `img-alt-${index}`,
        severity: "error",
        rule: "WCAG 1.1.1",
        message: "Image is missing an alt attribute.",
        selector: describe(img),
      });
    }
  });

  const buttons = root.querySelectorAll("button, [role='button']");
  buttons.forEach((el, index) => {
    const accessible =
      el.getAttribute("aria-label") ||
      el.getAttribute("aria-labelledby") ||
      (el.textContent ?? "").trim().length > 0;
    if (!accessible) {
      findings.push({
        id: `btn-name-${index}`,
        severity: "error",
        rule: "WCAG 4.1.2",
        message: "Interactive control has no accessible name.",
        selector: describe(el),
      });
    }
  });

  const iconsOnly = root.querySelectorAll("button.MuiIconButton-root");
  iconsOnly.forEach((el, index) => {
    if (!el.getAttribute("aria-label") && !(el.textContent ?? "").trim()) {
      findings.push({
        id: `icon-btn-${index}`,
        severity: "warning",
        rule: "WCAG 4.1.2",
        message: "Icon button should expose an aria-label.",
        selector: describe(el),
      });
    }
  });

  // Sample text contrast for body / caption-like nodes.
  const textNodes = root.querySelectorAll("p, span, label, h1, h2, h3, h4, h5, h6");
  let sampled = 0;
  textNodes.forEach((el, index) => {
    if (sampled >= 40) {
      return;
    }
    const style = window.getComputedStyle(el);
    if (style.display === "none" || style.visibility === "hidden") {
      return;
    }
    const ratio = contrastRatio(style.color, style.backgroundColor === "rgba(0, 0, 0, 0)" ? "#ffffff" : style.backgroundColor);
    sampled += 1;
    if (ratio != null && ratio < 4.5) {
      findings.push({
        id: `contrast-${index}`,
        severity: "warning",
        rule: "WCAG 1.4.3",
        message: `Possible low contrast (${ratio.toFixed(2)}:1). Verify against actual background.`,
        selector: describe(el),
      });
    }
  });

  if (!root.querySelector("[data-skip-link], a.skip-link")) {
    findings.push({
      id: "skip-link",
      severity: "info",
      rule: "WCAG 2.4.1",
      message: "Consider a skip link to main content.",
    });
  }

  const errors = findings.filter((f) => f.severity === "error").length;
  const warnings = findings.filter((f) => f.severity === "warning").length;
  const passed = Math.max(0, 20 - errors - Math.min(10, warnings));
  const score = Math.max(0, Math.min(100, 100 - errors * 12 - warnings * 3));

  return {
    generatedAt: new Date().toISOString(),
    score,
    passed,
    warnings,
    errors,
    findings,
  };
}

function describe(el: Element): string {
  const id = el.id ? `#${el.id}` : "";
  const cls =
    typeof el.className === "string" && el.className
      ? `.${el.className.split(/\s+/).slice(0, 2).join(".")}`
      : "";
  return `${el.tagName.toLowerCase()}${id}${cls}`;
}

export function formatAccessibilityReport(report: AccessibilityReport): string {
  const lines = [
    `Accessibility Report — score ${report.score}/100`,
    `Generated: ${report.generatedAt}`,
    `Errors: ${report.errors} · Warnings: ${report.warnings}`,
    "",
    ...report.findings.map(
      (f) => `[${f.severity.toUpperCase()}] ${f.rule}: ${f.message}${f.selector ? ` (${f.selector})` : ""}`,
    ),
  ];
  return lines.join("\n");
}
