# AI11.FIX.1.3 — Confidence Validation

**Change type:** Root-cause fix (already delivered by FIX.1.1/1.2) + DEBUG-only defensive assertion.
**File:** `Abhyanvaya.Infrastructure/InsightFace/InsightFaceEngine.cs` → `ParseDetectionOutputs()`
**No clamping. No threshold change.**

---

## Problem (from AI11.AI.3)

The old parser produced "confidences" up to **2.87**. SCRFD scores are **sigmoid activations** and
must satisfy `0.0 <= confidence <= 1.0`. Values above 1.0 proved that regression tensors (box
distances / landmark offsets) were being read as scores for strides 16 and 32.

## Root cause — fixed, not masked

The impossible values were a direct symptom of the tensor-mapping bug. FIX.1.1 (correct
score/box/kps grouping) and FIX.1.2 (correct anchor rows) ensure **confidence is read exclusively
from the score tensors**:

```csharp
const int scoreGroup = 0;
var scores = outputList[scoreGroup + strideIndex].AsEnumerable<float>().ToArray();
...
var score = scores[row];   // ONLY source of confidence — never a box/landmark tensor
```

Box distances come from `boxGroup` (outputs 3–5) and landmarks from `landmarkGroup` (outputs 6–8);
neither can flow into the confidence value. No clamping is applied — the values are now naturally in
range because the correct tensor is read.

## Defensive assertion (DEBUG only, zero Release overhead)

A `Debug.Assert` documents and enforces the invariant during development. `Debug.Assert` calls are
compiled out entirely in Release builds (they are `[Conditional("DEBUG")]`), so there is **no runtime
overhead in production**.

```csharp
// Confidence is read ONLY from the score tensor. SCRFD scores are sigmoid activations and
// therefore always lie in [0, 1]; a value outside that range means a regression tensor is being
// misread as a score.
var score = scores[row];
Debug.Assert(
    score >= -0.001f && score <= 1.001f,
    $"SCRFD confidence out of [0,1] range: {score} (stride {stride}, row {row}). " +
    "A non-score tensor is being interpreted as confidence.");
```

(The ±0.001 tolerance avoids false trips on floating-point edge values exactly at 0 or 1.)

## Verification (sample classroom image, session `fc2ab073…`)

| Metric | Before (buggy) | After (fixed) |
|--------|----------------|---------------|
| Max confidence | **2.8658** ❌ | **0.8992** ✅ |
| Avg confidence | 1.4491 ❌ | 0.8347 ✅ |
| Min confidence | 0.5008 | 0.5580 |
| All values in [0,1] | No | **Yes** ✅ |

## Guarantees

- ✅ Confidence sourced only from score tensors.
- ✅ Regression tensors can never be interpreted as confidence.
- ✅ No clamping — root cause fixed.
- ✅ `Debug.Assert` guards the invariant in DEBUG; no Release overhead.
- ✅ Threshold value (`0.5`) unchanged.
