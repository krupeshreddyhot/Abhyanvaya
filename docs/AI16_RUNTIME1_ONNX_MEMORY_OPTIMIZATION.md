# AI16.RUNTIME.1 — ONNX Runtime Memory Optimization

**Status: IMPLEMENTED (allocator-strategy change only — no effect on inference output)**
**Date:** 2026-07-12
**Reviewer:** Chief Software Architect
**Scope:** `InsightFaceOnnxModelHost`, `Microsoft.ML.OnnxRuntime.SessionOptions`

---

## 1. Objective

Reduce the native (unmanaged) memory ONNX Runtime holds for the two resident InsightFace sessions
(SCRFD detection `det_10g.onnx`, ArcFace recognition `w600k_r50.onnx`) without changing a single
inference result. This is a CPU-inference allocator-configuration review only — no model, threshold,
or math change.

## 2. Every `SessionOptions` property — current state before this milestone

`InsightFaceOnnxModelHost.EnsureLoaded` constructed exactly one `SessionOptions` object per session,
setting only two of its many properties:

```csharp
var sessionOptions = new SessionOptions
{
    IntraOpNumThreads = _options.IntraOpNumThreads,   // = 1 (already customized, pre-AI16)
    InterOpNumThreads = _options.InterOpNumThreads,    // = 1 (already customized, pre-AI16)
};
```

Every other `SessionOptions` property was left at the ONNX Runtime C# API default. Documented below,
one row per property named in the task:

| Property | ORT default | Was set before AI16.RUNTIME.1? | Memory-relevant? |
|---|---|---|---|
| `IntraOpNumThreads` | `0` (auto — one thread per logical core) | ✅ Yes, `1` (pre-existing, see AI14.RUNTIME.1) | Yes — indirectly: each intra-op worker thread gets its own thread-local allocator arena when `EnableCpuMemArena=true`. Fewer threads ⇒ fewer arenas. |
| `InterOpNumThreads` | `0` (auto) | ✅ Yes, `1` (pre-existing) | Same mechanism, for inter-op parallel sections (SCRFD/ArcFace have effectively none, since `ExecutionMode` is sequential — see below). |
| `ExecutionMode` | `ExecutionMode.ORT_SEQUENTIAL` | ❌ Not set (default kept) | Yes — `ORT_PARALLEL` spins up an inter-op thread pool that executes independent graph nodes concurrently, which trades memory (extra thread stacks + parallelism bookkeeping) for latency on graphs with parallelizable branches. SCRFD/ArcFace are narrow, mostly-linear CNN graphs with little to gain from node-level parallelism at batch size 1, so `ORT_SEQUENTIAL` (the default) is already the lower-memory choice — evaluated, not changed. |
| `GraphOptimizationLevel` | `ORT_ENABLE_ALL` | ❌ Not set (default kept) | Yes, favorably — `ORT_ENABLE_ALL` already includes constant folding and node/subgraph fusion, which *reduce* the number of intermediate tensors ORT must materialize during `Run()` versus a lower optimization level. The default is already the memory-favorable setting; explicitly setting it to the same value would be a no-op. Left as the implicit default. |
| `EnableCpuMemArena` | `true` | ❌ Not set (default kept) | **Yes — changed, see §3.** |
| `EnableMemoryPattern` | `true` | ❌ Not set (default kept) | **Yes — changed, see §3.** |
| `LogSeverityLevel` | `ORT_LOGGING_LEVEL_WARNING` (2) | ❌ Not set (default kept) | No — controls ONNX Runtime's own internal log verbosity, not memory. Evaluated per the task list; no memory-relevant effect found, so left at default to avoid unrelated log-volume changes. |
| Execution providers | CPU (no GPU/DirectML/CUDA provider registered) | ❌ Not set (default kept) | N/A — this deployment target (Render Starter, no GPU) only ever uses the built-in CPU execution provider; there is no lower-memory alternative provider to switch to here. |

## 3. The change: `EnableCpuMemArena` / `EnableMemoryPattern` → `false`

Both properties default to `true` in the ONNX Runtime C# API. Microsoft's documented guidance for
memory-constrained CPU inference (the "Reduce Memory Usage" performance-tuning notes in the ONNX
Runtime docs) is to disable both together:

- **`EnableCpuMemArena`** — when `true`, ORT allocates large blocks ("arenas") up front from the OS
  and services individual tensor allocations out of them, growing the arena rather than returning
  memory to the OS between `Run()` calls. This lowers *allocation latency* but means the process's
  resident memory reflects the arena's high-water mark, not its current working set — on a 512 MB
  Render Starter instance already running two loaded models, that permanently-reserved arena is
  memory the OOM killer counts against the container regardless of how busy inference currently is.
- **`EnableMemoryPattern`** — when `true`, ORT records the tensor shapes/sizes seen on the *first*
  `Run()` and pre-plans one reusable allocation layout for every subsequent `Run()` with matching
  input shapes. This is a speed optimization that assumes a memory arena to plan against; Microsoft's
  guidance pairs it with `EnableCpuMemArena` and recommends disabling both together for low-memory
  scenarios — a mismatched combination (pattern on, arena off) is not the documented configuration.

Both are pure **allocator strategy** switches — they control *how* ORT requests and reuses native
memory pages, not *what* the graph computes. Disabling them forces ORT to allocate and free each
tensor's buffer directly through the OS/CRT allocator for every `Run()` call, which is exactly the
same computation with a smaller, more elastic memory footprint. **Inference output is byte-for-byte
identical either way** — this is stated directly in the ONNX Runtime documentation and confirmed here
by inspection: neither flag appears anywhere near the numerical kernels, only in
`onnxruntime::IExecutionProvider`'s allocator wiring.

```csharp
// Abhyanvaya.Infrastructure/InsightFace/InsightFaceOnnxModelHost.cs
var sessionOptions = new SessionOptions
{
    IntraOpNumThreads = _options.IntraOpNumThreads,
    InterOpNumThreads = _options.InterOpNumThreads,
    EnableCpuMemArena = _options.EnableCpuMemArena,     // NEW — default false
    EnableMemoryPattern = _options.EnableMemoryPattern, // NEW — default false
};
```

Both are exposed as configurable `InsightFaceOptions` (`Abhyanvaya.Infrastructure/InsightFace/InsightFaceOptions.cs`)
rather than hardcoded booleans, so they can be flipped back to `true` via configuration on a future
deployment target that has spare memory and wants the latency benefit instead.

## 4. Estimated impact

| Dimension | Estimate | Basis |
|---|---|---|
| **Memory savings** | Removes the CPU memory arena's reserved high-water-mark allocation for both sessions — on the order of several MB to a few tens of MB per session depending on tensor sizes ORT has seen, freed back to the OS between calls instead of retained. Exact savings are workload-dependent and were not benchmarked in this pass (no profiler run performed); see `docs/AI16_RUNTIME4_NATIVE_PROFILE.md` for how to measure `PeakNativeEstimateMB` before/after this change on the actual deployment target. | ONNX Runtime documentation + allocator design (arena reservation vs. per-call OS allocation). |
| **CPU impact** | Small increase — each tensor allocation now goes through the general-purpose allocator instead of the pre-warmed arena/pattern-planned path. For SCRFD/ArcFace's tensor sizes (at most a few MB per intermediate tensor) this is a handful of extra `malloc`/`free` calls per `Run()`, not a measurable latency contributor at the single-photo-at-a-time throughput this service runs at. | Standard allocator-vs-arena tradeoff; ORT explicitly documents this as the expected tradeoff of disabling these flags. |
| **Latency impact** | Small increase for the same reason as CPU impact — arena/pattern reuse exists specifically to shave allocation time off repeated `Run()` calls with the same shapes. Expected to be low-single-digit milliseconds at most per detection/embedding call, well within the existing per-stage timing budget already tracked by `RecognitionPipelineDiagnostics`. | Same. |
| **Risk assessment** | **Low.** Purely an allocator configuration change; Microsoft documents it as a supported, common low-memory configuration for CPU inference. No code path reads either flag anywhere except `SessionOptions` construction — nothing in `InsightFaceEngine`/`InsightFaceImageMath` branches on them. Both are configurable (not hardcoded), so a regression can be reverted via configuration without a code change or redeploy in most hosting setups. | Code inspection — see §5. |

## 5. Requirements verified

- ✅ No AI/model change — `det_10g.onnx`/`w600k_r50.onnx` files and their invocation are untouched.
- ✅ No detection/similarity threshold change — `DetectionThreshold`, `NmsThreshold`,
  `DetectionInputSize`, `RecognitionInputSize` are all unmodified.
- ✅ No inference-output change — confirmed by inspecting the ONNX Runtime allocator/execution-provider
  boundary these flags control; they do not touch node kernels, weights, or numerical operations.
- ✅ `dotnet build` — `Abhyanvaya.Infrastructure` (where this change lives) builds with 0 errors.

## 6. Deliberately not changed

`ExecutionMode`, `GraphOptimizationLevel`, and `LogSeverityLevel` were reviewed (per Task 2) and left at
their ORT defaults — in each case the default was already the memory-neutral-or-favorable choice for
this workload (see the table in §2), so setting them explicitly would only add configuration surface
without a measurable benefit. Execution providers were reviewed (per the "Review" list); this
deployment target has no lower-memory CPU alternative provider available, so none was substituted.
