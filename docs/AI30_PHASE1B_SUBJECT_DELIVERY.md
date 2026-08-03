# AI30.3B — Subject Delivery Types

**Entity:** `SubjectDeliveryType` (Theory, Laboratory, Tutorial, Workshop, Seminar, Project, Internship, Field Work, Online, Hybrid, Blended, Self Study)

**Subject extensions:** DeliveryTypeId, PreferredRoomFeatureId, RequiresAttendance, ExpectedCapacity (reuses RequiresRoomType / RequiresLabEquipment from 1A).

**API:** `api/scheduling/subject-delivery-types`  
**UI:** Scheduling → Subject Delivery  

**Validation:** Lab → lab room types; Theory → Classroom; Online → room optional.
