# AI31.8.1A — UX Guidelines (Admin Enterprise Dashboard)

## Principles

1. **Hero first** — Only the eight institutional KPIs that answer “is the college operating today?”
2. **Value first** — KPI cards lead with the number, then title, then short description.
3. **One refresh clock** — Timestamps live in the sticky toolbar unless a widget refreshes independently.
4. **Actionable attention** — Attention cards always expose impact + a clear Review Now path.
5. **No workflow change** — Visual polish must never alter attendance or scheduling behavior.

## KPI Card Anatomy

```
128                 ← large value
Students Present    ← medium title
▲ 4%                ← optional trend
Healthy             ← severity (omit routine “Information”)
```

- Entire card opens drill-down when a path exists.
- Overflow menu (⋮): Open, Open Module, Open Report, Help.

## Toolbar

Always visible (sticky):

| Control | Purpose |
|---------|---------|
| Last Updated | Last successful load clock |
| Next Refresh | Computed from interval (or — when paused/manual) |
| Refresh / interval / pause | Existing refresh controls |
| Filters / Export / Preferences | Unchanged behavior |

On tablet, collapse secondary controls behind **Tools**.

## Attention Required

Sort order: Critical (Red) → High (Orange) → Medium (Yellow) → Low/Info.

Each card:

- Severity icon  
- Title  
- Count  
- Impact  
- Suggested action text  
- Review Now button  

## Accessibility

- Cards with paths are keyboard-focusable (`role="link"`, Enter/Space).
- Sticky toolbar uses standard MUI focus rings.
- Severity conveyed with icon + color + text label (not color alone).
- Help drawer remains available via overflow menu.
- Prefer tabular numerals for clock displays.

## Do / Don’t

| Do | Don’t |
|----|-------|
| Reuse existing DTO fields | Add new APIs for polish |
| Filter/sort on the client | Change AttendanceSessionResolver |
| Keep empty states calm and clear | Repeat View Details on every card |
| Group quick actions by domain | Hide actions behind unauthorized paths (filter by permission) |
