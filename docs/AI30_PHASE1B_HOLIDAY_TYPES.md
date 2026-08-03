# AI30.4B — Holiday Types

Enhances Academic Calendar. Existing Holiday enum `HolidayType` retained.

**Entity:** `HolidayTypeCatalog` (table SchedulingHolidayTypes)  
**Holiday extensions:** HolidayTypeCatalogId, IsWorkingDayOverride, RequiresRescheduling, Colour, Priority  

**API:** `api/scheduling/holiday-types`  
**UI:** Scheduling → Holiday Types; Holidays page type/colour selectors  

Seed includes National, Festival, University, College, Department, Examination, Maintenance, Emergency/Weather Closure, Optional, Training Day.
