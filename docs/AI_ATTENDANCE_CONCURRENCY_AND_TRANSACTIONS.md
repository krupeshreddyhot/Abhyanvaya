# AI Attendance — Concurrency & Transaction Hardening (T1–T2)

## T1 — Optimistic Concurrency

- `RowVersion` on `AttendanceSession` and `AttendanceRecognition` is enforced at save time.
- `ConcurrencyExceptionHelper` maps `DbUpdateConcurrencyException` → `ConcurrencyConflictException`.
- Used by `AttendanceRecognitionReviewService`, `AttendanceSessionFinalizer`, and indirectly by summary sync saves.
- API returns **HTTP 409** via `AttendanceReviewExceptionMapper`:

```json
{
  "message": "The attendance session was modified by another user.",
  "code": "ConcurrencyConflict",
  "reloadRequired": true
}
```

### Teacher conflict scenario

1. Teacher A and B open the same session review.
2. Teacher A approves/finalizes first.
3. Teacher B submits a review or finalize → **409 Conflict** (no silent overwrite).

---

## T2 — Atomic Finalization

Finalization runs inside `IApplicationDbContext.ExecuteInTransactionAsync` using the EF Core execution strategy.

**Single transaction commits together:**

- Session summary counters (`SyncSessionSummaryAsync`)
- `Attendance` rows
- `AttendanceDetail` rows + `RecognitionSnapshotJson`
- `AttendanceSession` approved status

**On any failure** (including concurrency or constraint violations): full rollback.

`AttendanceBuilder` stages entities via navigation (`Attendance.Detail`) and does **not** call `SaveChangesAsync`. One save occurs at the end of finalization.

---

## Files

| Component | Role |
|-----------|------|
| `ConcurrencyConflictException` | Domain-level conflict signal |
| `ConcurrencyExceptionHelper` | EF → domain translation |
| `AttendanceReviewExceptionMapper` | HTTP 409 mapping |
| `ExecuteInTransactionAsync` | Atomic unit of work |

No schema changes. No API contract changes beyond structured 409 responses.
