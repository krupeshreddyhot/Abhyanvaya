# AI11.AI.2 — Detection Output Parsing Review

**Type:** Read-only investigation. No code changed.
**Verdict:** ❌ **The ONNX output tensors are parsed incorrectly.** The parser assumes the wrong tensor
ordering *and* ignores the 2-anchors-per-cell layout. This is the primary root cause of the wrong
face counts.

---

## 1. Tensors returned after inference (verified from `det_10g.onnx`)

Graph output order, exactly as `session.Run` returns them:

| Return idx | Name | Shape | Data type | Meaning | Stride |
|-----------|------|-------|-----------|---------|--------|
| 0 | `448` | `[12800, 1]`  | float32 | **score** | 8 |
| 1 | `471` | `[3200, 1]`   | float32 | **score** | 16 |
| 2 | `494` | `[800, 1]`    | float32 | **score** | 32 |
| 3 | `451` | `[12800, 4]`  | float32 | **bbox** (distance L,T,R,B) | 8 |
| 4 | `474` | `[3200, 4]`   | float32 | **bbox** | 16 |
| 5 | `497` | `[800, 4]`    | float32 | **bbox** | 32 |
| 6 | `454` | `[12800, 10]` | float32 | **landmarks** (5×(x,y)) | 8 |
| 7 | `477` | `[3200, 10]`  | float32 | **landmarks** | 16 |
| 8 | `500` | `[800, 10]`   | float32 | **landmarks** | 32 |

**Official SCRFD format:** outputs are **grouped by type** → `[score_s8, score_s16, score_s32,
bbox_s8, bbox_s16, bbox_s32, kps_s8, kps_s16, kps_s32]`, with **2 anchors per feature-map cell**.
The rows are anchor-major (each cell contributes 2 consecutive rows).

---

## 2. What the code actually does

```129:154:Abhyanvaya.Infrastructure/InsightFace/InsightFaceEngine.cs
    private List<InsightFaceImageMath.FaceCandidate> ParseDetectionOutputs(
        IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs,
        int inputSize,
        float scale,
        int padX,
        int padY,
        int originalWidth,
        int originalHeight)
    {
        var candidates = new List<InsightFaceImageMath.FaceCandidate>();
        var strides = new[] { 8, 16, 32 };
        var outputList = outputs.ToList();

        for (var strideIndex = 0; strideIndex < strides.Length; strideIndex++)
        {
            if (strideIndex * 3 + 2 >= outputList.Count)
            {
                break;
            }

            var scores = outputList[strideIndex * 3].AsEnumerable<float>().ToArray();
            var boxes = outputList[strideIndex * 3 + 1].AsEnumerable<float>().ToArray();
            var landmarks = outputList[strideIndex * 3 + 2].AsEnumerable<float>().ToArray();
            var stride = strides[strideIndex];
            var featureHeight = inputSize / stride;
            var featureWidth = inputSize / stride;
```

The code indexes `outputList[strideIndex*3 + {0,1,2}]` — i.e. it assumes the outputs are
**interleaved per stride**: `[score, bbox, kps, score, bbox, kps, …]`.

### BUG #1 — Wrong tensor→role mapping

| strideIndex | Code reads as `scores` | Code reads as `boxes` | Code reads as `landmarks` |
|-------------|------------------------|------------------------|----------------------------|
| 0 (stride 8)  | `out[0]` = score_s8 ✅ | `out[1]` = **score_s16** ❌ | `out[2]` = **score_s32** ❌ |
| 1 (stride 16) | `out[3]` = **bbox_s8** ❌ | `out[4]` = **bbox_s16** ❌ | `out[5]` = **bbox_s32** ❌ |
| 2 (stride 32) | `out[6]` = **kps_s8** ❌ | `out[7]` = **kps_s16** ❌ | `out[8]` = **kps_s32** ❌ |

**Correct mapping** for grouped output is:

```
scores    = out[strideIndex]        // 0,1,2
boxes     = out[strideIndex + 3]    // 3,4,5
landmarks = out[strideIndex + 6]    // 6,7,8
```

Consequences:
- **stride 16 & 32 "scores"** are actually *bbox distances* / *landmark offsets*. These regression
  values are frequently `> 0.5`, so the threshold check passes for huge numbers of non-faces →
  hundreds of false positives with confidences well above `1.0` (impossible for a real sigmoid score).
- **"boxes"** are read from score tensors (shape `[…,1]`), so `boxOffset = index*4` runs off the end
  almost immediately (`continue`), and the few that pass produce degenerate/garbage geometry.

### BUG #2 — Anchor count (2 per cell) ignored

```156:166:Abhyanvaya.Infrastructure/InsightFace/InsightFaceEngine.cs
            for (var y = 0; y < featureHeight; y++)
            {
                for (var x = 0; x < featureWidth; x++)
                {
                    var index = y * featureWidth + x;
                    if (index >= scores.Length || scores[index] < _options.DetectionThreshold)
                    {
                        continue;
                    }

                    var boxOffset = index * 4;
```

The loop iterates `featureHeight × featureWidth` cells (e.g. 80×80 = 6,400) and uses
`index = y*featureWidth + x`. But each score tensor has `cells × 2` rows (12,800). Because rows are
**anchor-major**, `index` addresses `cell = index/2, anchor = index%2` — so the spatial `(x, y)` used
to compute the anchor centre no longer corresponds to the row being read. Even with the correct
tensors, the box centres would be misplaced and only the first half of the grid scanned.

## 3. Where each field is read (index mapping)

For a candidate at flattened `index`:

| Field | Code source | Correct SCRFD source |
|-------|-------------|----------------------|
| score | `scores[index]` | `score[row]`, `row = cell*2 + anchor` |
| x1 | `(anchorX - boxes[index*4+0]*stride)/scale` | `(cx - d0)` where `d = bbox[row]*stride`, `cx = x*stride` |
| y1 | `boxes[index*4+1]` | `cy - d1` |
| x2 | `boxes[index*4+2]` | `cx + d2` |
| y2 | `boxes[index*4+3]` | `cy + d3` |
| confidence | `scores[index]` | `score[row]` |
| landmarks | `landmarks[index*10 + i*2 (+1)]` | `kps[row][2i]*stride + cx`, `kps[row][2i+1]*stride + cy` |

The **per-field arithmetic within a row is essentially correct** (distance-to-corner decode, `×stride`,
landmark offset + centre, letterbox reversal `−pad` then `/scale`). The failure is entirely in
**which tensor** and **which row** feed that arithmetic (Bugs #1 and #2).

## 4. Empirical confirmation

Running `det_10g.onnx` on the real classroom image from session
`fc2ab073-5bd9-46f0-b1c9-dee2a3f42b23` (see AI11.AI.6 for the full trace):

| Parser | After threshold | After NMS | Confidence range |
|--------|-----------------|-----------|------------------|
| **Current code** (bugs #1+#2) | 1,786 | **584** | 0.50 – **2.87** (impossible) |
| **Correct SCRFD** (grouped + 2 anchors) | 72 | **19** | 0.56 – 0.90 (valid) |

The impossible `>1.0` "confidences" are the tell-tale sign that regression tensors are being read as
scores.

## 5. Verdict

- ❌ Code is **not** reading the correct tensor indices.
- ❌ Anchor count (2/cell) is not handled.
- ✅ The intra-row decode math and letterbox reversal are correct once fed the right data.

**Fix direction (for a later change task, not done here):** map `scores=out[i]`, `boxes=out[i+3]`,
`kps=out[i+6]`; expand the loop to 2 anchors per cell (anchor-major rows); keep the existing
distance-decode + letterbox math.
