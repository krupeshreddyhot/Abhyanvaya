# AI31 — Architecture Review

## Verdict

AI31 correctly introduces a faculty operational workspace that composes existing scheduling, attendance resolution, AI22 review, and SignalR infrastructure without creating parallel attendance or timetable systems.

## Key decisions

1. **Composition over duplication** — `FacultyDashboardService` queries published timetable entries and attendance sessions; scoring/conflict/optimization engines untouched.
2. **Single attendance entry** — UI always navigates to existing `/attendance`; `AttendanceSessionResolver` remains the only Timetable vs Legacy decision point.
3. **SignalR reuse** — `/hubs/faculty` uses the same ASP.NET SignalR stack; timetable change history publishes notifications (no polling framework).
4. **Mobile reuse** — one responsive page following AI22.7C patterns (safe areas, touch targets, sticky actions).
