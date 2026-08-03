# AI30.2B — Enterprise Room Features

Normalizes room capabilities. **Room entity unchanged** (FeatureFlags retained).

**Entities:** `RoomFeature`, `RoomFeatureAssignment`  
**API:** `api/scheduling/room-features`, `api/scheduling/rooms/{roomId}/features`, clone-assignments  
**UI:** Scheduling → Room Features (catalog + chip assignment + clone)

Seeded features include Projector, Smart Board, AI Camera, labs, accessibility, etc. Duplicate assignments prohibited.
