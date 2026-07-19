# AI20.UI.7 — Dashboard Component Architecture Review

**Type:** Architecture review of the AI20.UI.1–UI.6 implementation. Scope is `abhyanvaya-ui/src/**` only — no API, service, repository, or database changes were made or reviewed here (Phase 1 explicitly excluded backend logic).

**Verdict up front:** No architectural defects were found that required a code change. Two minor, pre-existing (not introduced by this milestone) observations are recorded in §7 as recommendations only.

---

## 1. What was built (files)

| File | Kind | Delivers |
|---|---|---|
| `src/layouts/MainLayout.tsx` | Container (existing, extended) | AI20.UI.1 — `PsychologyIcon` + violet accent for the AI Center sidebar family |
| `src/components/common/AppBreadcrumbs.tsx` | Presentational, generic | AI20.UI.3 — data-driven breadcrumb trail |
| `src/components/common/StatCard.tsx` | Presentational, generic | AI20.UI.2 — one labelled metric tile ("Total Students", "--", icon) |
| `src/components/common/EmptyStateCard.tsx` | Presentational, generic | AI20.UI.2 / AI20.UI.6 — professional empty-state card (icon + title + description + optional action) |
| `src/components/ai/AiSystemStatusCard.tsx` | Presentational, AI-domain-generic | AI20.UI.5 — reusable subsystem health card with ready/starting/offline/unknown chips |
| `src/components/ai/AiModuleTabs.tsx` | Presentational, AI-domain-generic | AI20.UI.4 — Overview/History/Failures/Settings tab shell with disabled+tooltip support |
| `src/pages/ai/StudentEnrollmentPage.tsx` | Container | Composes all of the above into the dashboard shell (AI20.UI.2/3/4/6) |
| `src/pages/ai/AiCenterPage.tsx` | Container (pre-existing, untouched) | AI Center hub — unchanged in this milestone |

No file was duplicated, split, or renamed outside this list. `MainLayout.tsx`'s routing, `visible()` authorization predicates, and menu ordering are byte-for-byte unchanged except for the two lines that assign `PsychologyIcon` and the additive `accent?: "ai"` field (opt-in, defaults to the previous behavior for every other item).

---

## 2. Component diagram

```
AppRoutes
 └─ ProtectedRoute (allowedRoles=["SuperAdmin"])
     └─ MainLayout                              ← container: auth, routing, sidebar accent (AI20.UI.1)
         └─ <Outlet/>
             └─ StudentEnrollmentPage            ← container: owns activeTab state, mock data
                 ├─ AppBreadcrumbs                   (common, generic)
                 ├─ (header Typography — page-local, not extracted: see §4)
                 ├─ AiSystemStatusCard               (ai-domain, generic)
                 │    └─ MUI Card / Chip
                 ├─ AiModuleTabs                     (ai-domain, generic)
                 │    └─ MUI Tabs / Tab / Tooltip
                 └─ Overview tab content (page-local Box, not extracted: see §4)
                      ├─ StatCard × 5                (common, generic)
                      └─ EmptyStateCard               (common, generic)
                           └─ (Button — page-local "Start New Enrollment Batch")
```

Legend: **common** = domain-agnostic, reusable anywhere in the app. **ai-domain** = reusable across *any* future AI module page, but assumes the "subsystem health" / "Overview·History·Failures·Settings" shapes specific to AI modules. **page-local** = intentionally not extracted (see §4 for why).

---

## 3. Reuse matrix

| Component | Reused by (today) | Designed to be reused by (future) | Coupling to Student Enrollment |
|---|---|---|---|
| `AppBreadcrumbs` | `StudentEnrollmentPage` | Any future page needing a crumb trail (`items` is a plain `{label, to?}[]`) | **None** — takes no enrollment-specific props |
| `StatCard` | `StudentEnrollmentPage` (× 5, mapped from a config array) | Any dashboard summary tile anywhere in the app | **None** — `label`/`value`/`icon` are generic |
| `EmptyStateCard` | `StudentEnrollmentPage` (Recent Batches) | AI20.UI.6 explicitly calls out History, Failures, and Statistics empty states as future consumers once those tabs gain content; also a drop-in replacement for the ad-hoc empty-state text already in `ClassroomPhotoPanel.tsx` and `Login.tsx` (not migrated in this milestone — those files were out of scope) | **None** — `icon`/`title`/`description`/`action` are generic |
| `AiSystemStatusCard` | `StudentEnrollmentPage` | Any future AI module page (a hypothetical "Recognition Tuning" page would pass its own `items`) | **None** — takes an `AiSystemStatusItem[]`, has zero knowledge of "enrollment" |
| `AiModuleTabs` | `StudentEnrollmentPage` | Any future AI module page that wants the same Overview/History/Failures/Settings shell — the tab list is a prop, not hardcoded | **None** — takes a `tabs`/`value`/`onChange` triple, has zero knowledge of "enrollment" |

**Zero net-new duplication.** A repository-wide search confirmed there was no pre-existing breadcrumb, stat-tile, or empty-state component before this milestone (`AIStatusChip` and `AnimatedCount` in `components/common/` are pre-existing and address different concerns — see §5).

---

## 4. Presentation vs. container separation

- **Containers** (`MainLayout`, `StudentEnrollmentPage`) own state (`activeTab`), routing, data-fetch timing (none yet — mock data is a `const` at module scope, not `useState`/`useEffect`, so it's trivially clear nothing here is masquerading as "real" data), and composition. Neither renders raw MUI primitives for anything that has a reusable shape.
- **Presentational** components (`AppBreadcrumbs`, `StatCard`, `EmptyStateCard`, `AiSystemStatusCard`, `AiModuleTabs`) accept only plain props/callbacks, hold no state except `AiModuleTabs`' controlled `value` (owned by the caller), and make no network calls, no `useAuth()`/`useNavigate()` calls, and import nothing from `context/` or `services/`.
- **Deliberately not extracted:** the page header block (`<Typography variant="h4">Student Enrollment</Typography>` + subtitle) and the "Start New Enrollment Batch" button are left inline in `StudentEnrollmentPage`. Both are single-use, one-line JSX with no variability — wrapping them in a component today would add an indirection with no second caller and no prop surface worth naming. This mirrors the same judgment call already made throughout the codebase (e.g. `AiCenterPage.tsx`'s header).

---

## 5. Duplication check against the existing codebase

| Existing component | Domain | Why it was *not* reused / extended for this milestone |
|---|---|---|
| `components/common/AIStatusChip.tsx` | Attendance-recognition **workflow step** status (`Ready, Uploading, Processing, Matching, AwaitingReview, Completed, Failed, Cancelled, Pending, NotStarted, NotCreated`) | Semantically different axis: it answers "what step is this attendance job on?", not "is this AI subsystem healthy?". Its `AIStatus` enum has no `Starting`/`Offline`/`Unknown` members and several members (`Matching`, `AwaitingReview`) have no meaning for a subsystem-health card. Repurposing it would have forced either (a) polluting a workflow enum with infrastructure-health values it will never emit, or (b) a lossy re-mapping. `AiSystemStatusCard` defines its own narrow `AiSystemStatusLevel` (`ready \| starting \| offline \| unknown`) instead — same visual language (MUI `Chip`, `color` prop), different domain model. |
| `components/common/AnimatedCount.tsx` | Animated numeric counter | Not applicable yet — every summary value on this page is the literal string `"--"` (explicitly required placeholder text, not a number), so there is nothing to animate. **Noted for reuse**: once AI20.IMPLEMENT delivers a real `/ai/enrollment/summary` endpoint, `StatCard`'s `value` prop already accepts a `ReactNode`, so swapping `"--"` for `<AnimatedCount value={...} />` is a one-line change per tile with no `StatCard` changes needed. |

No component was duplicated; two adjacent-but-distinct existing components were evaluated and correctly left alone.

---

## 6. Compliance checklist

| Criterion | Status | Evidence |
|---|---|---|
| **Responsive layout** | ✅ | `StatCard` grid: `xs: 1fr 1fr → sm: repeat(3,1fr) → md: repeat(5,1fr)`. `AiSystemStatusCard` grid: `xs: 1fr → sm: repeat(2,1fr) → md: repeat(3,1fr)`. `AiModuleTabs` uses `variant="scrollable" scrollButtons="auto"` so 4 tabs never overflow on mobile. |
| **Accessibility** | ✅ (see minor note in §7) | `AppBreadcrumbs` sets `aria-label="breadcrumb"`. Disabled `AiModuleTabs` entries keep their `Tooltip` reachable via the MUI-recommended `<span>` wrapper (a disabled native element fires no pointer/focus events, so the tooltip would otherwise never appear). Status is never color-only: `AiSystemStatusCard` pairs every color with a distinct icon shape (`CheckCircle`/`Autorenew`/`Error`/`HelpOutline`) *and* a text `Chip` label, so a colorblind or greyscale-screen user still gets the information. The disabled "Start New Enrollment Batch" button is wrapped in `Tooltip`+`<span>` for the same reason. |
| **Dark mode compatibility** | ✅ for all net-new components | Every new component uses theme-relative tokens exclusively (`action.hover`, `text.secondary`, `text.disabled`, `divider`, MUI `Chip color="success/warning/error/default"`, `Card variant="outlined"`) — none hardcodes a hex color. They will repaint correctly the moment the app adds a dark `ThemeProvider` (it does not have one today — see §7). |
| **MUI best practices** | ✅ | Uses `sx` for one-off styling (no inline `style=`), `Card`/`CardContent`/`Stack` composition consistent with existing components (e.g. `SessionDashboardCard.tsx`), controlled `Tabs`/`Tab` pattern, `Tooltip` + `<span>` wrapper for disabled interactive elements (the documented MUI workaround). |
| **SOLID** | ✅ | *SRP*: each new component does exactly one thing (breadcrumb trail / metric tile / empty state / subsystem health / tab shell). *OCP*: `AiModuleTabs` and `AiSystemStatusCard` are extended by passing new `items`/`tabs` data, never by editing the component. *LSP/ISP*: prop interfaces are minimal and non-overlapping — no component accepts a prop it doesn't use. *DIP*: containers depend on the presentational components' prop **interfaces** (exported `type`s), not on each other's internals; none of the presentational components imports `AuthContext`, routing, or services. |
| **No duplicated components** | ✅ | See §5. |
| **Reusable cards / tables** | ✅ cards; **N/A** tables | `StatCard` and `EmptyStateCard` are reused today and designed for reuse elsewhere. No table exists yet in this milestone — "Recent Enrollment Batches" is empty (zero rows), so per AI20.UI.6 it renders `EmptyStateCard` instead of a table with only headers. The 8 required columns (Batch/Created/Status/Students/Embedded/Failed/Duration/Actions) are a documented contract for the **next** milestone that adds real batch rows, not a component that exists to review today (see §8). |

---

## 7. Findings (recommendations only — no code changed)

1. **Pre-existing, not introduced here:** `MainLayout.tsx`'s selected-row backgrounds (`#e3f2fd`/`#1976d2` for operational items, and the new `#f3e5f5`/`#6A1B9A` violet accent added for AI20.UI.1) are hardcoded hex values rather than theme tokens. This is consistent with the file's pre-existing convention — every other menu item already used a hardcoded hex pair before this milestone — so the new AI accent intentionally matched that convention rather than silently changing only the AI rows to a different (token-based) approach mid-file. **Recommendation:** if/when a dark theme is introduced app-wide, convert *all* sidebar selected-state colors (not just the new violet one) to `theme.palette.*` tokens in one pass, so the whole sidebar — not just the AI rows — stays consistent.
2. **Minor a11y polish opportunity:** decorative icons inside `EmptyStateCard` and `StatCard` (e.g. `Inventory2OutlinedIcon`, the metric-tile icons) do not set `aria-hidden`. This does not currently break anything (MUI `SvgIcon`s have no accessible name by default, so screen readers already skip them), but explicitly marking them `aria-hidden="true"` at the call site would be a small, non-breaking accessibility improvement worth doing app-wide (not specific to this milestone) rather than only on the two new components.

Neither finding blocks this milestone or required a code change per the task's constraint ("no code changes unless architectural issues are found") — both are pre-existing patterns or purely additive polish for a later, app-wide pass.

---

## 8. Future extensibility

- **New AI module page** (e.g. a hypothetical embedding-health page): reuse `AiModuleTabs` (pass its own tab list), `AiSystemStatusCard` (pass its own `items`), `StatCard`, `EmptyStateCard`, and `AppBreadcrumbs` as-is. Zero changes required to any of the five components.
- **Recent Enrollment Batches gains real rows:** the 8-column contract (Batch, Created, Status, Students, Embedded, Failed, Duration, Actions) becomes a new `EnrollmentBatchesTable` presentational component; when `batches.length === 0` it renders the *same* `EmptyStateCard` instance already built here (no new empty-state code), and when non-empty it renders a standard MUI `Table`. This was intentionally not built in this milestone — there is no data source yet, and building a table component against zero real rows risks guessing the wrong shape.
- **History / Failures / Settings tabs go live:** each becomes new tab-content JSX inside `StudentEnrollmentPage` (or is split into per-tab container components once they have real behavior), each free to reuse `EmptyStateCard` for its own empty state and `StatCard` for its own metrics, exactly as Overview does today.
- **Real summary numbers arrive:** swap each `StatCard`'s `value="--"` for `<AnimatedCount value={n} />` (already available in `components/common/`) — no `StatCard` prop changes needed since `value` is already typed `ReactNode`.
- **AI Center gains a third module:** `MainLayout`'s `MenuItem.children` array and the `accent: "ai"` flag already generalize to any number of AI submenu items with zero new sidebar code.

---

## 9. Addendum — AI20.UI.8–11 update

The four sections above describe the AI20.UI.1–UI.7 state. AI20.UI.8–UI.11 then redesigned the dashboard into a denser, more "enterprise console"-style layout. This addendum records what changed; §1–8 above remain accurate for everything **not** listed here.

### 9.1 File changes

| File | Change |
|---|---|
| `components/ai/AiSystemStatusCard.tsx` | **Redesigned (UI.8).** No longer a single `Card` containing a grid of *rows*; now renders an (optional) section heading followed by a CSS-grid (`repeat(auto-fit, minmax(190px, 1fr))`) of **one independent `Card` per subsystem** — the "service health tile" pattern used by Azure Portal/AWS Console/Datadog. `AiSystemStatusItem` gained `detail?: string` (provider/engine name, e.g. "ExamBranch") separate from `statusLabel?: string` (chip text, defaults to the level's label but can be overridden, e.g. "Running" for Background Worker) — this is a superset of the old `value: string` field, so no information was lost, only split into two independently-optional pieces. Public component name, props shape's `label`/`status`, and export are otherwise unchanged; only `StudentEnrollmentPage`'s mock data needed updating. |
| `components/common/KeyValueList.tsx` | **New (supports UI.9).** Generic read-only label/value list, extracted rather than hand-rolled inside `EnrollmentConfigurationCard` so any future read-only settings/config card can reuse it. |
| `components/ai/EnrollmentConfigurationCard.tsx` | **New (UI.9).** Composes `KeyValueList` inside a `Card`, exactly mirroring the composition style already used by `AiSystemStatusCard`/`StudentEnrollmentPage`. |
| `components/common/DisabledActionButton.tsx` | **New (supports UI.10/UI.11).** Extracted because AI20.UI.10 (hero CTA) and AI20.UI.11 (empty-state CTA) are two genuine, simultaneous callers of the same "disabled button + tooltip" shape — the second-caller bar from §4 of this review is now met, so extraction (rather than the page-local inlining used for the header block) is the correct call here. |
| `components/common/EmptyStateCard.tsx` | **Updated (UI.11).** Reduced vertical padding (`py: 3` → `py: 2.5`, tighter `Stack` spacing), and the icon now sits inside a 56px circle tinted with `alpha(theme.palette.primary.main, 0.08)` for a subtle "illustration" feel with zero external assets — still 100% theme-token-driven, so it repaints correctly the moment a dark `ThemeProvider` is added (see §6/§7). |
| `pages/ai/StudentEnrollmentPage.tsx` | **Reordered (UI.10) + densified (UI.11).** New order: breadcrumbs → header → **hero action** (`DisabledActionButton`, large, with a static "Available in Phase 2." caption beneath it, not just a hover tooltip) → `AiSystemStatusCard` → `EnrollmentConfigurationCard` → `AiModuleTabs` → Overview tab (summary `StatCard`s → `EmptyStateCard`, now carrying its own contextual `DisabledActionButton`). Outer `Stack` spacing tightened from `3` to `2.5` throughout. |

### 9.2 Reuse matrix update

Two more generic components joined the roster from §3, with genuine ≥ 2 callers each already inside this milestone (not speculative reuse):

| Component | Callers today | Coupling to Student Enrollment |
|---|---|---|
| `KeyValueList` | `EnrollmentConfigurationCard` | **None** — generic `{label, value, mono?}[]` |
| `DisabledActionButton` | `StudentEnrollmentPage` (hero) **and** the `EmptyStateCard` action slot in the same page (Recent Batches) | **None** — generic `label`/`tooltip`/`icon`/`variant`/`size` |

### 9.3 Compliance re-check

All checklist rows in §6 still hold. Two additions worth calling out explicitly:

- **Dark mode:** the new `EmptyStateCard` illustration circle uses `alpha()` from `@mui/material/styles` against `theme.palette.primary.main` rather than a hardcoded tint — this is strictly more dark-mode-correct than a hardcoded hex would have been, and continues the "no component hardcodes a color" property from §6.
- **Duplication:** `EnrollmentConfigurationCard`'s "read-only key/value panel" shape was checked against every existing card component (`SessionDashboardCard`'s `detailRows`, `RecognitionSummaryCard`, `FinalizationSummaryCard`) before extracting `KeyValueList` — none of them expose a reusable label/value-list primitive today (they're each single-purpose cards with bespoke internal layout), so `KeyValueList` is net-new, not a duplicate.

No architectural defects were found in this pass either; no additional code changes beyond the ones listed in §9.1 were required.

---

## 10. Addendum — AI20.UI.13–16 update

AI20.UI.13–UI.16 refined the configuration presentation, added two new read-only cards, and tuned density. This addendum records the deltas; §1–9 remain accurate for everything not listed here.

### 10.1 File changes

| File | Change |
|---|---|
| `components/ai/EnrollmentConfigurationCard.tsx` | **Redesigned (UI.13).** Replaced the single-column `KeyValueList` with a responsive MUI `Grid` (`size={{ xs:12, sm:6, lg:4 }}`) of icon+label+value tiles. Each tile has its own subsystem icon, a `caption` label above a `body2` bold value, on a soft `action.hover` surface. The long "Photo URL Template" row keeps monospace styling **and** spans full width (`size 12`) at every breakpoint so it never gets clipped into a narrow column. |
| `components/ai/EnrollmentWorkflowCard.tsx` | **New (UI.14).** Static 5-stage process flow (Download → Validate → Embedding → Storage → Recognition). Horizontal on `md+`, vertical on `xs`; connector arrows flip direction (`ArrowForward`/`ArrowDownward`) to match, and are `aria-hidden`. No progress/state/execution — purely a diagram. |
| `components/ai/AiTechnologyCard.tsx` | **New (UI.16).** Read-only "Current AI Stack" card. **Reuses `KeyValueList`** (which UI.13 had otherwise orphaned — see 10.3). "Recognition Threshold" deliberately renders a `Chip` reading "Configured" rather than a hardcoded number, per the milestone's explicit note that the real threshold may load dynamically later. |
| `components/common/StatCard.tsx` | **Densified (UI.15).** `CardContent` padding `→ p:1.5`, stack spacing `0.75 → 0.5`, value `h4 → h5`, label `noWrap`. Prop shape unchanged. |
| `pages/ai/StudentEnrollmentPage.tsx` | **Reflowed + densified (UI.15).** Outer `Stack spacing 2.5 → 2`; header + hero action now share one row (`space-between`) on `sm+` to reclaim vertical space; Configuration + AI Stack share a `2fr 1fr` grid row on `lg+` (stacked below); Workflow card added full-width beneath them; summary grid gap tightened `2 → 1.5`. Everything above "Recent Enrollment Batches" now fits a 1080p viewport (UI.15 target). |

### 10.2 Reuse matrix update

| Component | Callers today | Coupling to Student Enrollment |
|---|---|---|
| `KeyValueList` | `AiTechnologyCard` (was `EnrollmentConfigurationCard` before UI.13; see 10.3) | **None** |
| `DisabledActionButton` | `StudentEnrollmentPage` hero + `EmptyStateCard` action | **None** |

`EnrollmentConfigurationCard`, `EnrollmentWorkflowCard`, and `AiTechnologyCard` are all **module-specific composers** (they hardcode their own title + mock rows), each built from generic primitives (`Grid`/`Stack`/`Card`, and `KeyValueList` for the AI Stack card). This is the same deliberate trade-off documented for `EnrollmentConfigurationCard` originally — see the final-review's §10 "AI module scalability."

### 10.3 The orphaned-component question (UI.13)

UI.13's redesign of `EnrollmentConfigurationCard` stopped using `KeyValueList`, which would have left `KeyValueList` as dead code. Rather than delete it, it was **repurposed by `AiTechnologyCard` (UI.16)**, which is a genuine label/value list and a natural fit. Net result: `KeyValueList` still has exactly one live caller, no dead code was introduced, and the config card got the richer icon-grid treatment UI.13 asked for. This was a conscious sequencing decision (build UI.13 and UI.16 together) rather than an accident.

### 10.4 Compliance re-check

All §6 checklist rows still hold. Notes:
- **Responsive:** `EnrollmentConfigurationCard` (Grid `xs/sm/lg`), `EnrollmentWorkflowCard` (row↔column at `md`), and the page's Config+AIStack row (`lg` breakpoint) all add explicit tablet/desktop/large-monitor behavior per UI.15.
- **Dark mode:** both new cards use only theme tokens (`action.hover`, `text.secondary`, `alpha(theme.palette.primary.main, ...)`, `Chip color`), continuing the "no hardcoded hex in feature components" property.
- **Accessibility:** workflow connector arrows are `aria-hidden="true"` (decorative); status/threshold are conveyed by chip text, not color alone.

No architectural defects were found; no code changes beyond those listed in §10.1 were required.
