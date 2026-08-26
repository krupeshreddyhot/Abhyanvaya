# AI29.1D — Section Capacity Engine in Allocation Workspace

The Section Capacity step consumes the **Section Capacity Engine** APIs (`/api/sections/capacity/*`). The UI never computes authoritative occupancy.

## Displayed fields (per target section)

| Column | Source |
|--------|--------|
| Section | Capacity Engine snapshot |
| Capacity | `maximumCapacity` (+ min/recommended/reserved metadata) |
| Current Occupancy | `currentStrength` |
| Available Capacity | `availableSeats` |
| Occupancy % | `occupancyPercent` |
| Capacity Status | `capacityStatus` |

## Policy (engine)

From `GET /api/sections/capacity/policy`:

- Hard limit (`enforceHardLimit`)
- Soft limit (`softLimitEnabled`)
- Warning threshold (`warningPercent` / `autoWarningEnabled`)
- Under-capacity policy (`underCapacityPercent`)

Fallback: if capacity APIs are unavailable, the panel shows Allocation Context capacity projections (authored by the Capacity Engine during context build).

## Proposed over-capacity

After simulate/run, unsatisfied `Capacity` / `ReservedSeats` constraint evaluations are shown with **Mandatory** or **Preferred** labels. Mandatory violations disable Approve — no silent commit.
