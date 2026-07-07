# AI11.FIX.1.4 — Detection Verification

**Method:** The corrected parser logic (FIX.1.1 + 1.2 + 1.3) was executed against the actual
`det_10g.onnx` on the same classroom image used in AI11.AI.5/AI.6 (session
`fc2ab073-5bd9-46f0-b1c9-dee2a3f42b23`). The verification harness mirrors the new C# `ParseDetectionOutputs`
exactly (grouped tensor mapping, 2 anchors/cell, score-only confidence, same decode + letterbox +
NMS at the same thresholds).

---

## Image

| Property | Value |
|----------|-------|
| Resolution | 2048 × 1228 |
| Letterbox | scale = 0.31250, padX = 0, padY = 128 |
| Detection threshold | 0.5 (unchanged) |
| NMS threshold | 0.4 (unchanged) |

## Stage counts

| Stage | Count |
|-------|-------|
| Before threshold (candidates built) | **72** |
| After threshold (0.5) | **72** |
| After NMS (0.4) | **19** |
| **Final detected faces** | **19** ✅ |

## Confidence statistics

| Metric | Value |
|--------|-------|
| Average | **0.8347** |
| Minimum | **0.5580** |
| Maximum | **0.8992** |
| All in [0, 1] | **Yes** ✅ |

Expected: ~10–20 faces for this group photo → **19 is within range.** ✅

## Before vs after (same image, same model, same thresholds)

| Metric | Before (buggy parser) | After (corrected parser) |
|--------|-----------------------|--------------------------|
| Final faces (post-NMS) | 584 ❌ | **19** ✅ |
| Max confidence | 2.8658 ❌ | 0.8992 ✅ |
| Avg confidence | 1.4491 ❌ | 0.8347 ✅ |
| Boxes | 1px-tall, glued to edges | face-sized, on people |

## Annotated result

Green boxes = corrected detector output. Boxes align with actual people's faces.

![Corrected SCRFD detections](assets/AI11_FIX1_4_DETECTIONS.png)

Sample boxes (score, x1,y1,x2,y2):
```
0.8992  [1276.3, 472.0, 1319.9, 529.1]
0.8746  [225.4, 452.9, 280.5, 515.2]
0.8720  [366.5, 506.9, 413.1, 562.7]
0.8703  [938.7, 438.0, 985.6, 498.7]
...
0.5580  [1634.5, 430.2, 1654.3, 456.2]
```

## Conclusion

The corrected detector produces a realistic face count (19), valid confidences (all ≤ 1.0), and
correctly placed bounding boxes — meeting every acceptance criterion of AI11.FIX.1.
