# AI30.2A.1 — Schedule Versioning

**Entity:** `ScheduleVersion` → Timetable(s) → TimetableEntries  
**API:** `api/scheduling/versions`  
**UI:** Catalog → Scheduling → Schedule Versions  

Features: create, duplicate, clone previous, mark current, archive, history.  
Statuses: Draft → Under Review → Approved → Published → Archived.  
Additive: `Timetable.ScheduleVersionId` nullable.
