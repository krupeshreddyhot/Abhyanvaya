# AI11.FIX.1.1 — Correct SCRFD Output Tensor Mapping

**Change type:** Source fix (single method).
**File:** `Abhyanvaya.Infrastructure/InsightFace/InsightFaceEngine.cs` → `ParseDetectionOutputs()`
**Scope guard:** decode math, thresholds, NMS, scaling, matching, recognition — **unchanged**. Only
the tensor→role mapping (and anchor iteration, see AI11.FIX.1.2) was corrected. All public APIs
unchanged.

---

## Problem (from AI11.AI.2)

The previous parser assumed the 9 SCRFD outputs were **interleaved per stride**:

```
outputs[strideIndex*3 + 0] = score
outputs[strideIndex*3 + 1] = box
outputs[strideIndex*3 + 2] = kps
```

The real `det_10g.onnx` graph emits them **grouped by tensor type**:

| Output idx | Tensor | Stride | Shape |
|-----------|--------|--------|-------|
| 0 | scores | 8 | `[12800, 1]` |
| 1 | scores | 16 | `[3200, 1]` |
| 2 | scores | 32 | `[800, 1]` |
| 3 | boxes | 8 | `[12800, 4]` |
| 4 | boxes | 16 | `[3200, 4]` |
| 5 | boxes | 32 | `[800, 4]` |
| 6 | landmarks | 8 | `[12800, 10]` |
| 7 | landmarks | 16 | `[3200, 10]` |
| 8 | landmarks | 32 | `[800, 10]` |

## Fix

The parser now maps by **group base + strideIndex**:

```csharp
const int scoreGroup = 0;
var boxGroup = strides.Length;          // outputs[3..5]
var landmarkGroup = strides.Length * 2; // outputs[6..8]
...
var scores    = outputList[scoreGroup + strideIndex].AsEnumerable<float>().ToArray();
var boxes     = outputList[boxGroup + strideIndex].AsEnumerable<float>().ToArray();
var landmarks = outputList[landmarkGroup + strideIndex].AsEnumerable<float>().ToArray();
```

| strideIndex | scores | boxes | landmarks |
|-------------|--------|-------|-----------|
| 0 (stride 8)  | out[0] ✅ | out[3] ✅ | out[6] ✅ |
| 1 (stride 16) | out[1] ✅ | out[4] ✅ | out[7] ✅ |
| 2 (stride 32) | out[2] ✅ | out[5] ✅ | out[8] ✅ |

A guard was added to fail safe if the model ever returns fewer than 9 outputs:

```csharp
if (outputList.Count < strides.Length * 3)
{
    _logger.LogWarning("SCRFD detection model returned {OutputCount} outputs; expected at least {Expected}. ...",
        outputList.Count, strides.Length * 3);
    return candidates;
}
```

## What was NOT changed

- Distance-to-corner decode (`anchor ± dist*stride`) — identical.
- Letterbox reversal (`(coord − pad) / scale`) — identical.
- `DetectionThreshold` comparison — identical value & logic.
- `ApplyNms(..., NmsThreshold)` in `DetectFaces` — untouched.
- Landmark decode formula — identical.
- `FaceCandidate` shape, `DetectAsync`, `GenerateSingleFaceEmbedding` — unchanged.

## Result

With correct mapping (plus the two-anchor fix in AI11.FIX.1.2), the sample classroom image yields
**19** detections with valid confidences in [0,1] (was 584 with impossible >1.0 scores). See
AI11.FIX.1.4.

## Build

`Abhyanvaya.Infrastructure` compiles with **0 errors**. (A solution-wide build reported only
file-lock copy errors because the API was running under Visual Studio — not compilation errors.)
