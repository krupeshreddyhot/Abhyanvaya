# AI14.RUNTIME.5 — Heartbeat-Based Recovery (Future Architecture)

**Status: DESIGN ONLY — NOT IMPLEMENTED**
**Date:** 2026-07-11
**Reviewer:** Chief Software Architect

---

> **Scope note.** This document is an architectural design exercise only. **No code was changed and
> no database schema was changed to produce it.** Every code snippet below is illustrative
> ("what this would look like"), not a diff against the current codebase. Implementation is
> explicitly out of scope for AI14.RUNTIME.5 and is left for a future milestone (see §9).

---

## 1. Why this document exists

AI14.RUNTIME.1–4 (this same release) added a recovery sweep
(`StuckAttendanceSessionRecoveryService`) that detects `AttendanceSession` rows orphaned by a crashed
or OOM-killed worker, throttles how many it recovers per run, and exposes its own health/metrics. That
sweep — and the pipeline it protects — has one structural limitation worth addressing before AI
workloads grow: **it can only measure "time since the job started," not "is the job still alive."**
As classroom photos get larger (more students, more faces) or run on more constrained hardware, total
processing time will grow, and a fixed "started more than N minutes ago" rule cannot distinguish a
dead job from a slow-but-live one without either (a) risking false-positive recoveries of jobs that
are still legitimately working, or (b) setting the timeout so generously that real crashes go
undetected for a long time. A heartbeat-based design removes that trade-off entirely by measuring
liveness directly instead of inferring it from total elapsed time.

---

## 2. Current design

```
StartedUtc
   ↓
Timeout
   ↓
Failed
```

### 2.1 How it actually works today

- `ClassroomRecognitionPipeline.ProcessAsync` sets `session.StartedUtc = DateTime.UtcNow` once, at the
  moment it calls `session.MoveToProcessing()` — before face detection, matching, or embedding
  comparison begins.
- No further progress signal is ever written for the duration of that job. The next write to the
  session row is either the pipeline's own success path (`MoveToAwaitingReview`) or its own failure
  path (`catch` block → `MoveToFailed`) — both only reachable if the process is still alive to run
  that code.
- `StuckAttendanceSessionRecoveryService` (a separate background service, same process or a future
  separate process) periodically scans for rows where
  `Status == Processing && StartedUtc < UtcNow - TimeoutMinutes`, and — having no other signal
  available — treats "started more than `TimeoutMinutes` ago and still `Processing`" as proof the
  worker that owned it is gone, and moves the row to `Failed`.

### 2.2 The structural limitation

`TimeoutMinutes` is doing two jobs it cannot do well simultaneously:

1. **Crash detection latency** — how quickly do we notice a dead job? Smaller is better.
2. **Ceiling on legitimate processing time** — how long is any real job allowed to run? Larger is
   better, and this ceiling must grow as photos/classrooms get bigger.

Because both are driven by the same single number measured from the same single point in time
(`StartedUtc`), improving one necessarily worsens the other. A classroom photo that legitimately takes
12 minutes to process (large class, CPU-only inference, several dozen faces) on a `TimeoutMinutes: 10`
configuration would be **incorrectly recovered while the worker is still correctly running it** — the
sweep cannot tell "still working" apart from "worker is gone." This is not hypothetical: it is the
same class of resource-constrained InsightFace workload that produced the original stuck-at-45%/OOM
incident this recovery service exists to remediate; as classes grow or hardware stays constrained,
this failure mode gets *more* likely, not less, under the current design.

---

## 3. Proposed design

```
Worker
   ↓
Update LastActivityUtc
   ↓
Recovery Service
   ↓
No activity
   ↓
Recover
```

### 3.1 Concept

Replace "time since the job *started*" with "time since the job *last proved it was still alive*."
The worker periodically writes a heartbeat (`LastActivityUtc`) while it is actively processing a
session. The recovery service's only question becomes: **has this session's heartbeat gone stale?**
Total processing time becomes unbounded (or bounded by a much larger, separate safety ceiling — see
§3.4) as long as the worker keeps proving it is alive; a dead worker is detected within roughly one
heartbeat interval plus a grace multiplier, independent of how long the job had already been running.

### 3.2 Where heartbeats would be written

The natural anchor points inside `ClassroomRecognitionPipeline.ProcessAsync` (illustrative, not a
proposed diff):

- Once when the job transitions to `Processing` (heartbeat #0 — equivalent to what `StartedUtc` gives
  today).
- Once after face **detection** completes (a potentially slow, single ONNX call).
- Once per **N faces** during embedding/matching (not once per face — see §5 on write amplification).
- Once immediately before the final `MoveToAwaitingReview`/`MoveToFailed` transition.

The exact granularity is an implementation-time decision, not part of this design; the important
architectural property is that heartbeats are emitted from **inside the actual work loop**, so a
worker that has hung (e.g. stuck in a native ONNX call, deadlocked, or the process is still alive but
making no progress) also fails to heartbeat and is still detected — heartbeating must not become "yet
another timer that fires regardless of whether real work is progressing."

### 3.3 Where heartbeats would be read

`StuckAttendanceSessionRecoveryService`'s candidate query changes shape from:

```
Status == Processing && StartedUtc < now - TimeoutMinutes
```

to:

```
Status == Processing && LastActivityUtc < now - HeartbeatTimeout
```

`HeartbeatTimeout` would be a new, much smaller configuration value (e.g. tens of seconds to a few
minutes, tuned to a small multiple of the heartbeat interval) — decoupled from and typically far
smaller than today's `TimeoutMinutes`, which currently has to cover the *entire* worst-case job
duration.

### 3.4 What happens to `TimeoutMinutes`

It does not disappear; its job changes. Recommended split of responsibilities:

- **`HeartbeatTimeout`** (new, small) — "is the worker that owns this session still alive?" This is
  what actually drives recovery decisions once heartbeat data is present.
- **`AbsoluteTimeoutMinutes`** (repurposed from today's `TimeoutMinutes`, made deliberately generous,
  e.g. 60+ minutes) — a hard backstop for a pathological case where a worker heartbeats forever on a
  session that is stuck in an infinite loop making no real progress but somehow still executing the
  heartbeat statement. This is a defense-in-depth ceiling, not the primary detection mechanism.

---

## 4. Database impact

| Change | Detail |
|---|---|
| New column | `AttendanceSession.LastActivityUtc` (nullable `timestamp`). Nullable so existing rows and any in-flight session at deploy time are valid without a backfill. |
| Write pattern change | Today, a session row is written twice during a job (start, end). With heartbeats, it is written `O(heartbeat count)` times per job — bounded by design (§3.2, §5), not by face count. |
| Read pattern | Unchanged — the recovery sweep still does one `COUNT` + one bounded `SELECT ... ORDER BY ... LIMIT MaxRecoveriesPerRun` per scan; only the `WHERE` predicate's column changes. |
| Index | The existing implicit need for an index supporting `Status == Processing` filtering (already true today for `StartedUtc`) extends to `LastActivityUtc`. A composite index on `(Status, LastActivityUtc)` would be worth adding at implementation time; not required for this design doc. |
| Symmetry gap (existing, called out not fixed) | `StudentFaceEmbedding` has no equivalent `StartedUtc`/heartbeat field at all today — the embedding pipeline has no orphan-recovery mechanism whatsoever, heartbeat-based or otherwise. If AI15 adopts heartbeats for classroom recognition, extending the same column/mechanism to student embedding is a natural, low-risk follow-on (out of scope here). |

No schema change is included in AI14.RUNTIME.5 itself; the column above is what a future
implementation milestone would add via a standard additive EF Core migration.

---

## 5. Performance

The central performance risk of heartbeating is **write amplification**: naively heartbeating once
per detected face would turn a 40-face classroom photo into 40 extra `UPDATE` statements against the
same row, vs. 2 today (start, end). Recommended mitigations, in order of impact:

1. **Time-boxed heartbeats, not per-item heartbeats.** Heartbeat on a timer (e.g. "if ≥15s elapsed
   since the last heartbeat, write one") checked at natural loop boundaries, rather than unconditionally
   on every face. This bounds write frequency by wall-clock time, not by face count, regardless of how
   large a classroom photo becomes.
2. **A narrow, non-transactional write path.** A heartbeat should update exactly one column
   (`LastActivityUtc`) and should not go through the full EF Core change-tracking + domain-event +
   optimistic-concurrency machinery used for real state transitions (`MoveToProcessing`,
   `MoveToFailed`, etc.). A targeted `ExecuteUpdateAsync` (EF Core 7+) or equivalent lightweight
   statement avoids loading/tracking the full aggregate for what is otherwise a very hot, very cheap
   write.
3. **Exclude the heartbeat column from the optimistic concurrency token.** See §6 — this is as much a
   concurrency-correctness decision as a performance one.
4. **No heartbeat when nothing is running.** Idle sessions never heartbeat; the write volume this
   introduces is strictly proportional to concurrently *processing* jobs, which on the current
   single-consumer in-memory queue is naturally capped at 1 at a time.

With these mitigations, expected added write load is on the order of "a few extra small `UPDATE`s per
minute per concurrently-processing job" — negligible next to the actual recognition workload (ONNX
inference, image decode/encode) that dominates a job's wall-clock time and resource usage.

---

## 6. Concurrency

- **Heartbeat writes vs. the session's own state-transition writes (same process).** These originate
  from the same pipeline execution and the same logical unit of work; no new cross-process race is
  introduced here.
- **Heartbeat writes vs. the recovery sweep's read (`COUNT`/`SELECT`).** Read-only on the sweep's side;
  no conflict.
- **Heartbeat writes vs. the recovery sweep's recovery write (`MoveToFailed`).** This is the
  interesting case, and the existing design already has the right safety net for it:
  `StuckAttendanceSessionRecoveryService.TryRecoverSessionAsync` already saves each session
  individually and already treats a `DbUpdateConcurrencyException` as "this row changed since I read
  it — skip it, don't fail the batch" (via `ConcurrencyExceptionHelper`). If `LastActivityUtc`
  participates in the same optimistic concurrency token (`RowVersion`) as the rest of the row, a
  worker's heartbeat landing between the sweep's `SELECT` and its `Failed` `UPDATE` would cause exactly
  this conflict — and exactly this "skip it" behavior — which is the *correct* outcome (the job proved
  it was alive after all; don't fail it).
- **However:** if `LastActivityUtc` **is** part of the concurrency token, then *every* heartbeat also
  changes `RowVersion`, which increases the odds of spurious conflicts for anything else that might
  concurrently touch the same row (e.g. a faculty member's concurrent edit to the session, unrelated to
  recovery). **Recommendation:** exclude `LastActivityUtc` from the concurrency token (`RowVersion`)
  entirely, and instead have the recovery sweep's `Failed` transition itself re-check
  `LastActivityUtc` freshness immediately before committing (read-recheck-write), rather than relying
  on the concurrency token to catch a race on that specific column. This keeps heartbeats cheap and
  non-conflicting while still closing the race correctly and explicitly.
- **Future multi-instance scale-out (not the case today).** Today exactly one process consumes the
  in-memory `IClassroomPhotoQueue`, so exactly one writer ever heartbeats a given session — no
  distributed-lease problem exists yet. If the platform ever scales the recognition worker
  horizontally (multiple instances, each with their own in-memory queue, or a shared durable queue),
  heartbeats alone do not prevent two instances from picking up the same session; that requires a
  separate ownership/lease concept (e.g. a `LeaseOwner`/`LeaseExpiresUtc` pair). This is a related but
  distinct problem from what AI14.RUNTIME.5 is scoped to solve, and is flagged here only so it is not
  mistaken for being solved by heartbeats alone.

---

## 7. Backward compatibility

- **Additive-only schema.** `LastActivityUtc` would be nullable; no existing column is removed,
  renamed, or retyped; no existing query that doesn't reference it is affected.
- **Mixed-version rollout window.** During a rolling deploy, it is possible for the recovery service
  (new code) to be running before every worker instance has been redeployed with heartbeat-writing
  code, or vice versa. The recovery predicate must therefore tolerate `LastActivityUtc IS NULL`
  gracefully for the transition window — recommended behavior: if `LastActivityUtc` is null, fall back
  to the legacy `StartedUtc`-based check for that row, rather than either ignoring it (risking it never
  being recovered) or immediately flagging it (risking a false positive against an old-code worker
  that simply hasn't been updated to heartbeat yet).
- **No breaking change to `/health`, `/health/ready`, or the AI14.RUNTIME.4 recovery metrics
  contract.** `PendingRecoveries`, `RecoveredSessions`, etc. keep the same meaning; only the internal
  staleness predicate they're computed from changes.

---

## 8. Migration strategy

1. **Schema migration** — add nullable `LastActivityUtc` to `AttendanceSession`. Zero behavior change;
   column is unused by any code path immediately after this step.
2. **Worker starts writing, recovery still reads the old signal** — deploy pipeline code that writes
   heartbeats, while `StuckAttendanceSessionRecoveryService` continues to key off `StartedUtc` only.
   This "dark launch" validates real write volume/latency/lock behavior in production with zero risk
   to recovery correctness, and can be left running for a full observation window before proceeding.
3. **Recovery switches to a dual-signal predicate** — `LastActivityUtc`-based when present, legacy
   `StartedUtc`-based fallback when null (per §7). This is the first step where heartbeat data actually
   influences which sessions get recovered.
4. **Retire the legacy fallback (candidate for AI15, conditionally — see §10)** — once every session
   that reaches `Processing` reliably receives at least heartbeat #0 (see §3.2) and a full release
   cycle has passed with no regression, the `StartedUtc` fallback branch can be removed and
   `HeartbeatTimeout` becomes the sole recovery predicate.

Each step above is independently deployable and independently reversible (rolling back any single step
does not require rolling back the others), which is the main reason to sequence them this way rather
than shipping schema + worker + recovery-logic changes in one release.

---

## 9. Rollout plan

| Phase | What ships | Risk if it goes wrong | Rollback |
|---|---|---|---|
| 0 | Migration only (§8.1) | Effectively none — unused column | Drop column (or leave it; harmless) |
| 1 | Heartbeat writes, dark (§8.2) | Extra write load higher than modeled (§5) | Revert worker deploy; column stays, harmless |
| 2 | Dual-signal recovery predicate (§8.3) | A bug in the new predicate mis-recovers or under-recovers sessions | Revert recovery-service deploy to `StartedUtc`-only; no data loss (recovered sessions are `Failed`+retryable, not deleted) |
| 3 | Retire legacy fallback (§8.4, conditional) | A code path exists where a session reaches `Processing` without ever heartbeating, and now has no safety net | Re-enable the fallback branch (kept in source, feature-flagged rather than deleted, until this phase is proven stable) |

Each phase should be observed for at least one full production cycle (including at least one real
large-classroom-photo job and, ideally, one deliberate/synthetic crash test) before proceeding to the
next, using the AI14.RUNTIME.4 recovery metrics (`runs`, `recoveredSessions`, `pendingRecoveries`) plus
the AI14.RUNTIME.3 structured per-recovery logs as the observation signal.

---

## 10. Failure scenarios under the proposed design

| Scenario | Current (`StartedUtc`) behavior | Proposed (`LastActivityUtc`) behavior |
|---|---|---|
| Worker OOM-killed mid-job (the original incident) | Recovered, but only after the full `TimeoutMinutes` elapses from job *start* — slow relative to when the crash actually happened if the crash occurs late in a long job. | Recovered within roughly one heartbeat interval + grace of the crash, regardless of how long the job had already been running — faster, and the detection latency no longer grows with total job duration. |
| Worker legitimately slow but alive (large classroom, constrained CPU) | **False positive risk**: can be incorrectly recovered mid-flight once elapsed time exceeds `TimeoutMinutes`, even though the worker is fine. | Not recovered, as long as heartbeats keep landing — this is the core problem this design solves. |
| Worker hung (alive process, but stuck/deadlocked making no progress) | Eventually recovered once `TimeoutMinutes` elapses — same as a real crash, from the sweep's point of view. | Also recovered — as long as heartbeats are emitted from inside the active work loop (§3.2) and not from an independent timer, a hang also stops heartbeats. This must be an explicit implementation requirement, not an incidental property. |
| Heartbeat write itself fails (transient DB blip) while processing is otherwise fine | N/A — no heartbeat exists today. | Risk of a false-positive recovery if failures repeat past `HeartbeatTimeout`. Mitigate with (a) heartbeat write retries/backoff, (b) a `HeartbeatTimeout` grace multiplier (e.g. 3× the intended interval) rather than a razor-thin margin, and (c) logging every failed heartbeat write so this is diagnosable rather than silent. |
| Gap between `Processing` transition and the first heartbeat | N/A — `StartedUtc` itself covers this window today. | Covered by treating the `Processing` transition itself as heartbeat #0 (§3.2) — there is never a genuinely un-heartbeated moment for a healthy worker. |
| Mixed-version rolling deploy (old worker, new recovery service, or vice versa) | N/A | Covered by the `LastActivityUtc IS NULL` → fall back to `StartedUtc` rule (§7); no session becomes permanently unrecoverable or falsely recovered purely because of version skew during a deploy. |
| Recovery service itself down/disabled | Orphaned sessions accumulate, unrecovered, until it's back — same today as proposed. | Unchanged — heartbeating is a signal *for* the recovery service to consume; it doesn't change what happens when the recovery service itself isn't running. |
| Future multi-instance worker scale-out | N/A (single consumer today) | Not solved by heartbeats alone — see §6's note on leasing; flagged as a follow-on concern, not a regression introduced by this design. |

---

## 11. Recommendation: should heartbeat become mandatory in AI15?

**Yes, but as the primary signal with a retained safety-net fallback — not an unconditional, all-or-
nothing cutover.**

Concretely, for AI15:

- **Ship phases 0–3 from §9 (migration → dark-launch writes → dual-signal recovery) as the default,
  primary recovery mechanism.** The core architectural benefit — decoupling crash-detection latency
  from total job duration — is valuable now and only becomes more valuable as classroom sizes and
  photo complexity grow; there is no reason to defer it once designed.
- **Do not delete the `StartedUtc`-based fallback in AI15.** Keep it as the null-heartbeat safety net
  described in §7/§8.4, feature-flagged so it can be disabled once a full production cycle confirms no
  session ever reaches `Processing` without heartbeating. Treat "fully retire the fallback" as an
  AI16+ candidate, gated on that observation window, not a same-release action.
- **Extend the same mechanism to `StudentFaceEmbedding` only after classroom recognition has proven
  stable on it** (§4's symmetry gap) — do not ship both simultaneously in AI15; sequencing reduces the
  number of moving parts under observation at once.

This gives AI15 the durability improvement (faster, more accurate orphan detection; no more
false-positive recoveries of slow-but-alive jobs) while keeping the exact same "always have a
non-destructive rollback path" discipline used throughout AI11–AI14's recovery/observability work.

---

## 12. Summary

| Aspect | Current (`StartedUtc`) | Proposed (`LastActivityUtc`) |
|---|---|---|
| Signal measured | Time since job started | Time since job last proved it was alive |
| Detection latency for a real crash | Up to `TimeoutMinutes`, regardless of when the crash occurred | ~one heartbeat interval + grace, regardless of total job duration |
| False-positive risk on long-but-healthy jobs | Grows as workloads grow | Effectively eliminated, as long as heartbeats originate from the active work loop |
| Schema change | None (already shipped) | One new nullable column |
| Rollout risk | N/A | Fully phased, independently reversible at each step (§9) |
| Recommendation | — | Adopt as primary in AI15; retain fallback; extend to embeddings later |

**No code or database changes were made as part of producing this document**, per the AI14.RUNTIME.5
constraints.
