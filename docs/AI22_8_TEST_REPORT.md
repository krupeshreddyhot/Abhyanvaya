# AI22.8 Test Report

| Area | Coverage |
|------|----------|
| Workflow mapper | Unit tests |
| Resume path / no auto-start recognition | Unit tests |
| Retry completed-stages flag | Unit tests |
| Lifecycle expire guard / ApplyLocal | Unit tests |
| AttendanceSessionResolver contract | Unit guard |
| Pending / dashboard / expiration | Manual + API smoke after migration |

**Latest:** `AI228AttendanceRecoveryTests` — **11 passed**. API + UI production builds succeeded.

Target: expand toward 95% with integration tests after DB migration in CI.
