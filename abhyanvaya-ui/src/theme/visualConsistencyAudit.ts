/**
 * AI22.7B Phase 5.7 — visual consistency audit checklist (documentation helper).
 */

export type VisualAuditItem = {
  id: string;
  surface: string;
  token: string;
  status: "aligned" | "legacy" | "todo";
  notes: string;
};

export const VISUAL_COMPONENT_AUDIT: VisualAuditItem[] = [
  { id: "cards", surface: "Paper / Card", token: "shape.borderRadius + elevation.raised", status: "aligned", notes: "MuiPaper defaults via ThemeManager" },
  { id: "dialogs", surface: "Dialog", token: "radius.lg", status: "aligned", notes: "MuiDialog paper radius" },
  { id: "buttons", surface: "Button", token: "density.controlHeight", status: "aligned", notes: "minHeight from workspace density" },
  { id: "typography", surface: "Typography scale", token: "enterpriseTypography", status: "aligned", notes: "h1–caption mapped" },
  { id: "spacing", surface: "Stack/Grid gaps", token: "enterpriseSpacing", status: "aligned", notes: "theme.spacing factor" },
  { id: "toolbars", surface: "StickyReviewToolbar", token: "recognition.toolbar*", status: "aligned", notes: "Uses theme paper + chips" },
  { id: "icons", surface: "IconButton", token: "touch 44px", status: "aligned", notes: "Touch density expands hit area" },
  { id: "status-chips", surface: "Chip", token: "semanticColors", status: "aligned", notes: "success/warning/error/info" },
  { id: "recognition-badges", surface: "EnterpriseConfidenceBadge", token: "recognition.confidence*", status: "legacy", notes: "Still uses util constants; prefer palette.recognition next" },
  { id: "progress", surface: "LinearProgress", token: "primary", status: "aligned", notes: "Theme primary" },
  { id: "analytics", surface: "ReviewAnalyticsDashboard", token: "semanticColors", status: "aligned", notes: "Bar colors from palette keys" },
  { id: "ops-context", surface: "OperationalContextBanner", token: "info/primary", status: "aligned", notes: "Existing MUI Alert patterns" },
  { id: "filmstrip", surface: "FilmstripNavigator", token: "gallerySelected", status: "aligned", notes: "Primary border for selection" },
  { id: "gallery", surface: "ClassroomPhotoCollectionPanel", token: "gallery*", status: "aligned", notes: "Selection outline uses primary" },
];

export function summarizeVisualAudit(items: VisualAuditItem[] = VISUAL_COMPONENT_AUDIT) {
  return {
    total: items.length,
    aligned: items.filter((i) => i.status === "aligned").length,
    legacy: items.filter((i) => i.status === "legacy").length,
    todo: items.filter((i) => i.status === "todo").length,
  };
}
