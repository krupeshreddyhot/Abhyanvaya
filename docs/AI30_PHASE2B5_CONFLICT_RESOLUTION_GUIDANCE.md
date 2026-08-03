# AI30 Phase 2B.5.1 — Conflict Resolution Guidance

## Purpose

Enterprise **advisory** conflict resolution guidance. Teachers choose manually. The platform never auto-fixes or edits the timetable.

## Ownership

| Concern | Owner |
|---------|--------|
| Detection | `ConflictEngine` + `IConflictRule` plugins |
| Recommendations | `IConflictResolutionAdvisor` + providers |

## Types

- `IConflictResolutionAdvisor` / `ConflictResolutionAdvisor`
- `ConflictRecommendation` (Intelligence namespace)
- `ResolutionOption`, `ResolutionScore`, `ResolutionReason`
- DTO: `ConflictResolutionDto`

## Providers (pluggable)

- `RoomSwapRecommendationProvider`
- `FacultySwapRecommendationProvider`
- `TimeSlotRecommendationProvider`

## Guarantees

- Suggestions never modify timetable
- No optimizer / no automatic scheduling
- Every recommendation includes Confidence, Impact, Difficulty, Estimated Resolution

## UI

Conflict Details → Suggested Resolutions → Teacher chooses manually (`ConflictWorkspacePage` Explain dialog).
