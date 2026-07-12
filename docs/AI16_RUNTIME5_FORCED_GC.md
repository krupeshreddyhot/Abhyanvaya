# AI16.RUNTIME.5 — Forced GC Validation

**Status: IMPLEMENTED (diagnostics only, default OFF — no pipeline behavior change when disabled)**
**Date:** 2026-07-12
**Reviewer:** Chief Software Architect
**Scope:** `RecognitionDiagnosticsOptions.ForceGcValidation`, `RecognitionPipelineDiagnostics.LogForcedGcValidation`

---

## 1. Objective

Determine, on demand, whether elevated memory observed after a recognition job is genuinely
collectible managed garbage (which a GC pass reclaims) or native/unmanaged memory (which it cannot) —
without ever running this expensive check by default.

## 2. Configuration gate

```json
// appsettings.json
"RecognitionDiagnostics": {
  "Enabled": true,
  "WorkingSetWarningThresholdMB": 450,
  "ForceGcValidation": false
}
```

```csharp
// RecognitionDiagnosticsOptions.cs
public bool ForceGcValidation { get; set; } = false;
```

**Default is `false`.** This is read exactly once per job, inside `Complete()`, and the entire forced-GC
block is skipped with a single boolean check when the flag is off — there is no measurable cost at all
in the default (disabled) configuration beyond that one field read:

```csharp
private void LogForcedGcValidation()
{
    if (!_options.ForceGcValidation)
    {
        return;
    }
    // ... forced GC pass + before/after logging, only reached when explicitly enabled ...
}
```

## 3. What happens when enabled

Called only from `Complete()` — **never** from `Fail()` — immediately after the existing
"Recognition Memory Summary"/"Recognition Timing Summary" logs, i.e. only on a *successfully completed*
recognition job:

```csharp
var before = RecognitionMemorySnapshot.Capture();

GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();

var after = RecognitionMemorySnapshot.Capture();
```

This is precisely the sequence requested: a full blocking collection, a wait for any pending
finalizers (so finalizable native handles get a chance to release before the second measurement), then
a second collection to reclaim anything the first pass's finalizers just made collectible.

Before/after values are logged for all three dimensions named in the task:

```
----------------------------------------------------------
Forced GC Validation (diagnostics only — RecognitionDiagnostics:ForceGcValidation)
----------------------------------------------------------
  Managed Heap Before                 : 38.2 MB
  Managed Heap After                  : 4.1 MB
  Managed Heap Reclaimed               : 34.1 MB
  Working Set Before                  : 187.4 MB
  Working Set After                   : 152.9 MB
  Working Set Reclaimed                : 34.5 MB
  Private Memory Before               : 201.6 MB
  Private Memory After                : 168.2 MB
  Private Memory Reclaimed             : 33.4 MB
  Interpretation                      : Working Set/Private drops after a forced GC ⇒ that memory was collectible managed garbage. Memory that stays elevated after this forced GC is native/unmanaged (ONNX Runtime arena, OS-level allocator fragmentation, etc.) and a GC pass cannot reclaim it.
----------------------------------------------------------
```

(Illustrative values — not captured from a live run in this documentation pass; see
`docs/AI16_RUNTIME4_NATIVE_PROFILE.md` §5 for why, and how to obtain real numbers.)

## 4. Interpreting the result

- **Working Set/Private Memory drop substantially after the forced GC** ⇒ the elevated memory the job
  left behind was managed garbage that a normal (non-forced) GC would eventually reclaim on its own
  schedule anyway — this milestone's forced pass just makes that reclaim happen immediately and
  visibly, for investigation purposes.
- **Working Set/Private Memory stay elevated after the forced GC** ⇒ that memory is native/unmanaged —
  most likely the ONNX Runtime CPU memory arena (see `docs/AI16_RUNTIME1_ONNX_MEMORY_OPTIMIZATION.md`;
  disabling `EnableCpuMemArena`/`EnableMemoryPattern` is the direct mitigation this milestone already
  applied for that specific cause), OS-level heap fragmentation, or another native allocator a managed
  GC pass has no visibility into. This is the "smoking gun" signal that further native-memory
  investigation (rather than managed-allocation tuning) is the productive direction.

## 5. Why this is diagnostics-only and default-off

`GC.Collect()` followed by `GC.WaitForPendingFinalizers()` followed by another `GC.Collect()` is a
full, synchronous, **blocking** collection — it stops all other managed work on the process while it
runs. Doing this on every single job (dozens of times per classroom photo batch) would itself directly
hurt the CPU/latency budget this whole AI16.RUNTIME milestone exists to protect, and is never an
appropriate steady-state production setting. The flag exists purely so an operator can temporarily
enable it on the actual Render deployment while investigating a specific OOM/memory incident, read the
before/after evidence from the logs, and then flip it back off — it is a diagnostic instrument, not a
tuning knob meant to be left on.

## 6. Requirements verified

- ✅ Diagnostics only — the forced GC pass only runs inside `LogForcedGcValidation()`, called only from
  `Complete()`, and every measurement is a read (`RecognitionMemorySnapshot.Capture()`) plus a log call;
  nothing about the recognition result, matching, or persisted data is touched.
- ✅ Behind configuration — `RecognitionDiagnostics:ForceGcValidation`, exactly the key name specified.
- ✅ Default `FALSE` — confirmed in both `RecognitionDiagnosticsOptions` (C# default) and
  `appsettings.json` (explicit `false`, added for visibility even though it matches the C# default).
- ✅ Wrapped in the same try/catch-and-log pattern as every other `RecognitionPipelineDiagnostics`
  method — a failure inside the forced-GC block (e.g., an unexpected exception from the GC APIs
  themselves) is caught by `SafeLogInternalFailure` and can never propagate into `Complete()`'s caller.
- ✅ `dotnet build` — `Abhyanvaya.Infrastructure` builds with 0 errors.
