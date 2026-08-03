# AI31.5.1 — Faculty Calendar Integration

## Scope
Export-only calendar integration over existing timetable data.

## Endpoints
- `GET /api/faculty/workspace/calendar/ics` — download `.ics`
- `GET /api/faculty/workspace/calendar/subscribe.ics` — Outlook/Google “from URL” feed
- `GET /api/faculty/workspace/calendar/meta` — subscription hints

## Non-goals
- No two-way sync
- No Google/Outlook write-back
- No duplicate timetable generation

## UI
Faculty Workspace → Calendar tab: Export ICS, Print/PDF, subscription URL copy.
