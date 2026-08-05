# AI31.6 — Verification Report

## Automated

- `AI31_6_EnterpriseDashboardTests` — widget catalogs, preference apply, safety flags, resolver guard.

## Manual checklist

1. Faculty login → `/dashboard` shows Faculty Command Center; `/faculty` still works.
2. Admin login → `/dashboard` shows Enterprise Operations sections + widgets + charts.
3. Take Attendance still lands on existing `/attendance` (resolver decides mode).
4. Faculty without timetable → Legacy Course→Group→Semester→Subject→Period path unchanged.
5. Faculty with timetable → Today timetable → Attendance unchanged.
6. Notifications pin/dismiss/archive persist after refresh (DB state).
7. Preferences compact/hide/reorder survive reload.
8. Health Center shows Green/Yellow/Red components.
9. Analytics export CSV + print PDF.
10. SignalR update refreshes Command Center / Notification Center without polling.

## Schema

Run `scripts/Apply_AI31_6_DashboardSchema.sql` before preference/notification state persistence in PostgreSQL.
