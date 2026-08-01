# AI30.1B — Faculty Teaching Preferences

Scheduling metadata only — not timetable generation.

**Entity:** `FacultyTeachingPreference` (reuses Staff / AcademicYear; no duplicate Faculty).  
**API:** `api/scheduling/faculty-preferences`  
**UI:** Catalog → Scheduling → Faculty Preferences (tabs: General, Location, Subjects, Time, Advanced)

**Validation:** Max continuous classes > 0; min break ≥ 0; first≤last period; no duplicate active Staff+Year preferences.
