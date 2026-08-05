# AI31.7.5 — Enterprise Operations Dashboard UX Enhancement

## Architecture review

AI31.7.5 is a **composition + UI polish** layer on top of the AI31.7 Enterprise Operations Command Center.

| Layer | Role |
|-------|------|
| `OperationsCommandCenterService` | Composes existing recovery, analytics, scheduling, governance, conflicts, readiness, health, notifications, preferences |
| `EnterpriseOperationsCommandCenterDto` | Section hierarchy, action banners, refresh interval, safety flags |
| `DashboardWidgetDto` | Rich KPI fields (unit, comparison, trend, suggested action, impact, group, status label) |
| `AdminOperationsDashboardPage` | Responsive layout, 60s refresh, banners, grouped attendance, section icons |
| `DashboardWidgets` | Rich cards, tooltips, keyboard + context menu (View Details / Open Module / Open Report) |

**Out of scope (unchanged):** `AttendanceSessionResolver`, Attendance APIs, Scheduling engines, Recovery workflow, AI22 recognition, Optimization engine, Governance workflow.

## Widget hierarchy

1. Action banners (permission-aware, dismissible)
2. 🚨 Attention Required (severity-sorted)
3. 📅 Today's Operations (live 60s refresh)
4. 🗓 Timetable Operations
5. 📝 Attendance Operations (grouped)
6. 🎓 Academic Resources
7. 🖥 College System Health
8. ⚡ Quick Actions

## Business terminology mapping

| Avoid | Use |
|-------|-----|
| Conflict Count | Scheduling Issues Requiring Attention |
| Pending Recovery | Attendance Recovery Queue |
| Recognition Queue | AI Attendance Recognition Queue |
| Optimization Queue | Timetable Optimization Suggestions |
| Approval Queue | Timetable Approval Queue |
| Platform Health | College System Health |
| Draft Timetables | Draft Timetable Versions |
| Scheduling Operations | Timetable Operations |
| System Health | College System Health |
| Green / Yellow / Red (health UI) | Healthy / Warning / Critical |

## Dashboard hierarchy / section descriptions

- **Attention Required** — actionable operational queues sorted Critical → High → Medium → Information; severity colors Red / Orange / Yellow / Blue(Info) / Green.
- **Today's Operations** — current time, indicative academic period, running/remaining classes, faculty teaching, completion/rate, events/holidays.
- **Timetable Operations** — active year/version, drafts, approvals, scheduling issues, optimization suggestions, last published.
- **Attendance Operations** — Running Sessions → Recognition → Review → Recovery → Completed.
- **Academic Resources** — catalog counts with Catalog drill-downs and trend/comparison where available.
- **College System Health** — API, Database, SignalR, Recognition, Notifications, Background Jobs, Storage, Scheduler + heartbeat/incident/uptime.
- **Quick Actions** — permission-aware shortcuts (A/R/V/T/P/O/G/N).

## Responsive layout

| Width | Columns (typical) |
|-------|-------------------|
| &lt;600px (mobile) | 1 |
| 600–900 (tablet) | 2 |
| 900–1200 (1366) | 3 |
| 1200–1920 | 3–4 |
| 1920–2560 | 4 |

Wider page container (`maxWidth: 2560`) reduces unused whitespace on large monitors. No widget overlap; grid uses `minmax(0, 1fr)`.

## Navigation flow

Every card supports:

- **View Details** → widget `path` / drill-down map
- **Open Module** → same module path
- **Open Report** → `reportPath` when present
- Hover tooltips, keyboard Enter/Space, right-click context menu

## Role visibility

- Endpoint remains Admin-only (`AdminOnly`).
- Quick Actions and banners filter by `requiredPermission`.
- Preferences still load for Admin landing; composition-only.

## Accessibility

- Section accordion headers with `aria-controls` / `id`
- Cards: `role="link"`, `tabIndex`, `aria-label`
- Banner dismiss via labeled icon button
- Focus-visible outline on KPI cards
- Severity conveyed by icon + chip label (not color alone)

## Performance considerations

- Single Command Center fetch; **60s** silent refresh (minimum 30s guard)
- Sequential backend composition (shared DbContext safety)
- No new repositories; no attendance polling loops
- Banner dismissals stored in `localStorage`

## Backward compatibility

- Section codes (`attention`, `today`, `scheduling`, …) unchanged for prefs/collapse keys (new collapse key `ai31.7.5.*`)
- Legacy `GET admin/operations` still available
- AI31.7 DTO property names retained (`SchedulingOperations` property; title displayed as Timetable Operations)

## Attendance compatibility

| Mode | Support |
|------|---------|
| Legacy Course→…→Period | Unchanged — Take Attendance quick action |
| Timetable → Attendance | Unchanged — resolver selects mode |
| `AttendanceSessionResolver` | **Not modified** |

Flags: `CompositionOnly`, `DoesNotModifyAttendanceApis`, `DoesNotModifyAttendanceSessionResolver`, `SupportsLegacyAndTimetableAttendance`.

## Testing

- `AI31_7_5_EnterpriseDashboardUxTests` — terminology, icons, rich fields, banners, groups, refresh, safety, resolver guard
- Existing `AI31_7_OperationsCommandCenterTests` remain valid for codes/safety

## Prompt map

| Prompt | Focus |
|--------|-------|
| 1 | Business terminology |
| 2 | Attention severity sort + rich cards |
| 3 | Live Today's Operations (60s) |
| 4 | Academic Resources + Catalog links |
| 5 | Attendance visual groups |
| 6 | Timetable Operations rename/content |
| 7 | College System Health labels |
| 8 | Responsive 2/3/4 column layout |
| 9 | Rich KPI cards |
| 10 | Action banners |
| 11 | Navigation / a11y / context menu |
| 12 | Docs + tests + copy pack |
