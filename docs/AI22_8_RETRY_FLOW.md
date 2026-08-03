# AI22.8 Retry Flow

Stage-aware retries call existing `IClassroomPhotoService` requeue / finalizer.

| Kind | Behavior |
|------|----------|
| RetryRecognition / RetryEntireSession | Requeue session recognition (failed/pending only) |
| RetryFailedImages / RetryUpload | Requeue one image or session |
| RetryFinalization | Call existing finalizer |

Completed recognition images and finalized attendance are never restarted.
Retry history stored in `AttendanceRetryHistory` + `IAuditService`.
