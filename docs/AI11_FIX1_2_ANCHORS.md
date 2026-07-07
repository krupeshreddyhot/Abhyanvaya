# AI11.FIX.1.2 — SCRFD Two-Anchors-Per-Cell Support

**Change type:** Source fix (same method as FIX.1.1).
**File:** `Abhyanvaya.Infrastructure/InsightFace/InsightFaceEngine.cs` → `ParseDetectionOutputs()`
**Scope guard:** decode logic, confidence filtering, landmark decoding, box scaling, thresholds —
**unchanged**. Only the anchor iteration changed.

---

## Problem (from AI11.AI.2)

The official SCRFD `det_10g` model uses **2 anchors per feature-map cell**. The score/box/landmark
tensors therefore have `cells × 2` rows, laid out **anchor-major** (a cell's two anchors are
consecutive rows):

```
row = (y * featureWidth + x) * numAnchors + anchor
```

Confirmed by the tensor shapes at 640×640 input:

| Stride | Grid | Cells | × anchors | Rows (tensor length) |
|--------|------|-------|-----------|----------------------|
| 8 | 80×80 | 6,400 | ×2 | 12,800 |
| 16 | 40×40 | 1,600 | ×2 | 3,200 |
| 32 | 20×20 | 400 | ×2 | 800 |

The previous parser iterated only `featureHeight × featureWidth` cells and addressed rows with
`index = y*featureWidth + x` — i.e. **one anchor per cell**. This both mis-addressed rows (only the
first half of each tensor) and misaligned the anchor centre used for decoding.

## Fix

An inner loop now iterates **both anchors** per cell, and the flattened row index is computed
anchor-major. Both anchors of a cell share the same anchor centre `(x*stride, y*stride)` — exactly as
InsightFace constructs `anchor_centers` (each centre duplicated `numAnchors` times).

```csharp
const int numAnchors = 2;
...
for (var y = 0; y < featureHeight; y++)
{
    for (var x = 0; x < featureWidth; x++)
    {
        var cellIndex = y * featureWidth + x;
        for (var anchor = 0; anchor < numAnchors; anchor++)
        {
            var row = cellIndex * numAnchors + anchor;   // anchor-major row
            if (row >= scores.Length) { continue; }

            var score = scores[row];
            ...
            var boxOffset = row * 4;       // was index * 4
            var landmarkOffset = row * 10; // was index * 10
            // anchor centre still (x*stride, y*stride) — shared by both anchors
        }
    }
}
```

### Index mapping: before vs after

| Aspect | Before | After |
|--------|--------|-------|
| Rows scanned (stride 8) | 6,400 (half) | 12,800 (all) |
| Row for `(x,y,anchor)` | `y*fw + x` | `(y*fw + x)*2 + anchor` |
| Box offset | `index*4` | `row*4` |
| Landmark offset | `index*10` | `row*10` |
| Anchor centre | `(x*stride, y*stride)` | `(x*stride, y*stride)` (unchanged, shared) |

## What was NOT changed

- Distance decode, landmark decode, letterbox reversal, clamping — identical arithmetic.
- `DetectionThreshold` / `NmsThreshold` — untouched.
- Public APIs and `FaceCandidate` — unchanged.

## Result

Correct anchor coverage plus correct tensor mapping (FIX.1.1) produce **72** raw candidates → **19**
after NMS on the sample image, with all confidences in [0,1]. Full numbers in AI11.FIX.1.4.
