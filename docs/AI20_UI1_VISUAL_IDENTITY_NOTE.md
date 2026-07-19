# AI20.UI.1 — AI Center Visual Identity Note

**Type:** Brief architecture note (deliverable of AI20.UI.1). No routes, permissions, menu ordering, or backend code were changed.

---

## What changed

| Before | After |
|---|---|
| `SmartToyIcon` (generic robot glyph, same visual family as every other operational sidebar icon) | `PsychologyIcon` (documented fallback: `AutoAwesomeIcon`, if a future icon-package trim ever removes `Psychology`) |
| Selected-row style identical to every other menu item: `#e3f2fd` background / `#1976d2` (blue) icon+text | AI Center and its "Student Enrollment" child use a dedicated violet accent: `#f3e5f5` background / `#6A1B9A` icon+text, **both at rest and when selected** |
| Same icon size/alignment as the rest of the menu | Unchanged — `PsychologyIcon` renders inside the same `ListItemIcon` slot every other item uses, so spacing/alignment is identical |

Implementation: `abhyanvaya-ui/src/layouts/MainLayout.tsx`. A new `accent?: "ai"` flag was added to the existing `MenuItem` type (backwards compatible — every other item omits it and renders exactly as before). Two constant style objects (`OPERATIONAL_SELECTED_SX`, `AI_SELECTED_SX`) are picked per-row based on that flag; no routing, no `visible()` authorization predicate, and no menu ordering logic was touched.

## Why AI modules get their own visual identity

1. **Different mental model.** Operational modules (Dashboard, Students, Attendance, Reports, Catalog, Organization) are things a College Admin or Faculty user configures or reviews directly. AI modules are automated subsystems (background photo enrollment today; future embedding re-generation, recognition tuning, etc.) that a SuperAdmin *supervises* rather than operates day-to-day. A distinct color family lets a SuperAdmin's eye jump straight to "AI-related" capability in a sidebar that will keep growing.
2. **Blast-radius signal.** AI modules touch external services (photo providers, embedding engines, background workers) and cross-tenant data by design. Visually separating them is a lightweight reminder that these screens behave differently (SuperAdmin-only, cross-college) from the tenant-scoped screens above them.
3. **Room to grow without renaming again.** Because the accent is driven by a single `accent: "ai"` flag on `MenuItem` rather than being hand-coded per row, every future AI module (e.g. a hypothetical "Recognition Tuning" or "Embedding Health" page) automatically inherits the same violet identity by setting the same flag — no new colors to invent, no risk of visual drift between AI screens.
4. **Zero coupling to authorization.** The accent is purely presentational. Whether a row is *visible at all* is still governed exclusively by each item's `visible()` predicate (`role === "superadmin"`), which is completely unchanged — visual identity and authorization are independent concerns and this change keeps them that way.

## Screenshot

A live screenshot of the rendered sidebar/dashboard could not be captured in this session: the app's route guard (`ProtectedRoute`) and menu visibility are driven by a real SuperAdmin JWT decoded client-side from `localStorage`, and no SuperAdmin credentials were available in this environment to log in legitimately. Fabricating a token to bypass login was intentionally avoided rather than done as a shortcut for this deliverable.

To verify visually: run `npm run dev` in `abhyanvaya-ui/` and log in with a real SuperAdmin account — the "AI Center" row (and its "Student Enrollment" child) will render in violet (`#6A1B9A` icon/text on `#f3e5f5` when selected) while every other row remains the existing blue.
