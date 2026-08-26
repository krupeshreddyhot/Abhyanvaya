# AI29.1D Prompt 17 — UI/UX & Responsive Design

## Goal

Align AI29.1D academic/operational surfaces with the **existing AI31 Enterprise Dashboard** visual language and in-repo enterprise tokens.  
**No new design system.** ADL volumes 00–12 are not present in this repository; AI31.8.* docs + `enterpriseTokens` / `dashboardLayoutTokens` are the practical standard.

## Reused foundations

| Source | Usage |
|--------|--------|
| `dashboardLayoutTokens.fluidDashboardSx` | Page max-width / padding |
| `dashboardLayoutTokens.sectionAccent` | Left-accent panels |
| `theme/enterpriseTokens` + MUI theme | Spacing, density, semantics |
| `theme/tabletExperience.TOUCH_TARGET_PX` | Mobile/tablet control height |
| `EmptyStateCard` | Empty tables/lists |
| MUI `Alert` / `Dialog` / `Tooltip` | Error, confirm, contextual help |

## Shared academic chrome (new)

Under `abhyanvaya-ui/src/components/academic/`:

- `academicUiTokens.ts` — thin re-export + status chip mapping
- `AcademicOperationalPageShell` — title, breadcrumb, feedback, fluid shell
- `AcademicScopeToolbar` — sticky densified scope toolbar
- `AcademicDataPanel` — card + loading/empty + horizontal table scroll
- `AcademicConfirmDialog` — replaces `window.confirm`
- `AcademicStatusChip` / `AcademicHelpHint` — dense chips + tooltip help

## Surfaces updated

| Surface | Changes |
|---------|---------|
| Sections | Shell, scope toolbar, data panel, confirm delete, status chips |
| Allocation Context | Shell, toolbar, capacity panel, status chips |
| Enterprise Allocation Workspace | Breadcrumb + scrollable stepper on xs |
| Faculty Section Allocation | Help hint, assign card, data panel, chips |
| Timetable Hub | Shell, toolbar filters, data panel, confirm delete |
| Attendance | Fluid shell, help, densified chips (mobile save bar retained) |
| Faculty Workspace | Fluid shell, help, accent class cards, touch targets |

## Responsive priorities

- **Desktop:** admin workflows — sticky scope toolbar, wide tables in scroll hosts  
- **Tablet:** no horizontal page overflow; tables scroll inside panels; touch ≥ 44px  
- **Mobile:** attendance + faculty operational actions prioritized (existing sticky bars kept)

## Explicit non-goals

- No parallel theme or component library  
- No attendance/allocation business-logic changes  
- No invented hierarchy labels (breadcrumb still API-backed)
