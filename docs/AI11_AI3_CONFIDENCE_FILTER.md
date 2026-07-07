# AI11.AI.3 — Confidence Threshold Logic Review

**Type:** Read-only investigation. Thresholds NOT modified.
**Verdict:** The `DetectionThreshold = 0.5` **is wired to configuration and is applied** at parse
time. However, because of the tensor-mapping bug (AI11.AI.2), for strides 16 & 32 the threshold is
compared against *regression values instead of scores*, so filtering is effectively meaningless for
2 of the 3 strides.

---

## 1. Is the config value actually used?

**Yes.** `InsightFace:DetectionThreshold` → `InsightFaceOptions.DetectionThreshold` (bound via
`IOptions<InsightFaceOptions>`), consumed directly in the parse loop.

- `appsettings.json`: `"DetectionThreshold": 0.5`
- `InsightFaceOptions.DetectionThreshold` default: `0.5f`

```156:164:Abhyanvaya.Infrastructure/InsightFace/InsightFaceEngine.cs
            for (var y = 0; y < featureHeight; y++)
            {
                for (var x = 0; x < featureWidth; x++)
                {
                    var index = y * featureWidth + x;
                    if (index >= scores.Length || scores[index] < _options.DetectionThreshold)
                    {
                        continue;
                    }
```

There is no hard-coded literal; the option is read every iteration. So the threshold is **not
skipped** and its value (0.5) is genuinely applied.

## 2. Filtering pipeline

```
raw grid cells ──► [scores[index] < DetectionThreshold ? skip] ──► candidates ──► NMS
```

Confidence filtering happens **inline** during output parsing (there is no separate post-filter
stage). Only cells whose `scores[index] >= 0.5` become `FaceCandidate`s.

## 3. Observed numbers (real image, session `fc2ab073…`, 2048×1228)

Full trace in AI11.AI.6. Threshold-related counts:

### Current code (buggy tensor mapping)
| Metric | Value |
|--------|-------|
| Threshold used | **0.5** (from config) |
| Candidates surviving threshold | **1,786** |
| Confidence of survivors | 0.50 – **2.87** ← includes impossible >1.0 values |

First 20 "confidence" scores of survivors (post-NMS, descending): `2.87, 2.82, 2.78, 2.72, 2.67,
2.66, 2.63, 2.63, 2.62, 2.60, 2.59, 2.57, 2.57, 2.57, 2.57, 2.57, 2.56, 2.54, 2.54, 2.54`.
Real sigmoid scores can never exceed 1.0 — these come from bbox/landmark tensors misread as scores
(AI11.AI.2, Bug #1).

### Correct SCRFD parsing (for comparison)
| Metric | Value |
|--------|-------|
| Threshold used | 0.5 |
| Candidates surviving threshold | **72** |
| Confidence of survivors | 0.56 – 0.90 (valid) |

First 20 valid confidences (descending): `0.90, 0.87, 0.87, 0.87, 0.87, 0.87, 0.87, 0.86, 0.86,
0.86, 0.86, 0.86, 0.85, 0.85, 0.85, 0.85, 0.84, 0.64, 0.56` (only 19 survive NMS).

## 4. Is confidence filtering skipped?

**No — the code path executes and the config value is honoured.** But the *input* to the comparison is
corrupted for strides 16/32:

- stride 8: `scores[index]` reads the real score tensor → filtering works (subject to the anchor bug).
- stride 16: "scores" = `bbox_s8` distance tensor → threshold compares against distances.
- stride 32: "scores" = `kps_s8` landmark tensor → threshold compares against landmark offsets.

So the 0.5 gate is real, but for 2 of 3 strides it is filtering the wrong array, which is why 1,786
"detections" (many with confidence > 1.0) pass.

## 5. Verdict

| Question | Finding |
|----------|---------|
| Is `InsightFace:DetectionThreshold` used? | ✅ Yes, read from config each iteration |
| Threshold value applied | 0.5 |
| Is filtering skipped? | ❌ No, it runs |
| Is filtering meaningful? | ⚠️ Only for stride 8; strides 16/32 threshold regression tensors due to AI11.AI.2 bug |
| Evidence of corruption | Survivor "confidences" reach 2.87 (impossible for real scores) |

The threshold logic itself is correct; fixing AI11.AI.2 (tensor mapping) is required for it to filter
actual detection scores across all strides.
